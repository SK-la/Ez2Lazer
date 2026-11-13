// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez-style replacement for the official <see cref="osu.Game.Overlays.SkinEditor.SkinComponentToolbox"/>.
    /// Lists the components that can be placed into the active editing layer; clicking one places it,
    /// persists the layout, and selects it. Global and ruleset-specific libraries are separate
    /// instances of this section.
    /// </summary>
    public partial class EzSkinEditorComponentLibrarySection : EzSkinEditorSettingsGroup
    {
        private readonly bool rulesetScoped;

        [Resolved(canBeNull: true)]
        private EzSkinEditorHudLayer? hudLayer { get; set; }

        [Resolved(canBeNull: true)]
        private ISkinComponentToolboxFilter? typeFilter { get; set; }

        [Resolved]
        private EzSkinEditorSceneBar sceneBar { get; set; } = null!;

        /// <param name="rulesetScoped">
        /// When true the library lists ruleset-specific components for the ruleset the editor is editing;
        /// when false it lists global components only. The ruleset is resolved live from the HUD layer so
        /// it always matches the container the components are placed into.
        /// </param>
        public EzSkinEditorComponentLibrarySection(bool rulesetScoped)
            : base(rulesetScoped ? EzEditorStrings.LIBRARY_RULESET_TITLE : EzEditorStrings.LIBRARY_GLOBAL_TITLE)
        {
            this.rulesetScoped = rulesetScoped;
            Spacing = new Vector2(0, 8);
        }

        /// <summary>The ruleset whose components this library lists, or null for the global library.</summary>
        private RulesetInfo? resolvedRuleset => rulesetScoped ? hudLayer?.CurrentRuleset : null;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hudLayer == null)
            {
                showMessage(EzEditorStrings.LIBRARY_PLACEHOLDER);
                return;
            }

            rebuild();
            hudLayer.ActiveTargetChanged += rebuild;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            sceneBar.ActiveLayer.BindValueChanged(_ =>
            {
                var layer = sceneBar.ActiveLayer.Value;
                SectionVisible.Value = rulesetScoped
                    ? layer is EzHUDLayer.EzHudRuleset or EzHUDLayer.EzPlayfieldRuleset
                    : layer != EzHUDLayer.HudOff;
            }, true);
        }

        private void rebuild()
        {
            Clear();

            Add(new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = SectionTitle,
                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
            });

            Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = Colour4.White.Opacity(0.1f),
                Margin = new MarginPadding { Vertical = 2 },
            });

            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
            };

            Add(list);

            if (hudLayer == null)
                return;

            // A ruleset-scoped layer only accepts ruleset components; the global layer only global ones.
            // With no resolved ruleset yet, a mode library would otherwise list the global set under a
            // mode heading, so keep it empty until a ruleset is known.
            RulesetInfo? target = resolvedRuleset;

            if (rulesetScoped && target == null)
            {
                list.Add(emptyLabel());
                return;
            }

            foreach (var type in SerialisedDrawableInfo.GetAllAvailableDrawables(target))
                attemptAddComponent(list, type);

            if (list.Count == 0)
                list.Add(emptyLabel());
        }

        private OsuSpriteText emptyLabel() => new OsuSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = EzEditorStrings.LIBRARY_EMPTY,
            Font = OsuFont.Default.With(size: 13),
        };

        private void attemptAddComponent(FillFlowContainer list, Type type)
        {
            if (typeFilter?.IsExcluded(type) == true)
                return;

            try
            {
                Drawable instance = (Drawable)Activator.CreateInstance(type)!;

                if (!((ISerialisableDrawable)instance).CanBeSelected)
                {
                    instance.Dispose();
                    return;
                }

                // The probe instance is handed to the card and only enters the tree once the card is shown,
                // so creating the entry does not decode anything.
                list.Add(new ComponentPlaceButton(type, instance, hudLayer?.ActiveTarget)
                {
                    Action = () => place(type),
                });
            }
            catch (DependencyNotRegisteredException)
            {
                // The component needs dependencies this layer cannot provide; hide it from the library.
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Ez skin component {type} could not be listed in the library");
            }
        }

        private void place(Type type)
        {
            if (hudLayer?.TryPlaceComponent(type) != true)
                Logger.Log($"Could not place Ez skin component {type.Name}; the active layer rejected it.");
        }

        private void showMessage(LocalisableString text)
        {
            Child = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = text,
                Font = OsuFont.Default.With(size: 14),
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && hudLayer != null)
                hudLayer.ActiveTargetChanged -= rebuild;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Library entry: a live instance of the component (scaled to fit a thumbnail), its type name,
        /// and a click handler that places it into the active layer.
        /// </summary>
        /// <remarks>
        /// The preview is a live component, so it is only materialised while the card is on screen and is
        /// disposed once the card scrolls away or the group folds. Otherwise every available component would
        /// stay loaded (with its textures and update loops) for the whole editor session.
        /// </remarks>
        private partial class ComponentPlaceButton : EzButton
        {
            private const float thumbnail_height = 72;

            // Shorter than the framework default so a scrolling user sees the preview almost immediately.
            private const double time_before_load = 200;

            // Long enough that scrolling back and forth does not rebuild the preview each time.
            private const double time_before_unload = 2000;

            private Drawable? probePreview;

            public ComponentPlaceButton(Type type, Drawable probePreview, CompositeDrawable? dependencySource)
            {
                this.probePreview = probePreview;

                RelativeSizeAxes = Axes.X;
                Height = thumbnail_height + 24;

                Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = thumbnail_height,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = true,
                    Padding = new MarginPadding(8),
                    Child = new DelayedLoadUnloadWrapper(() => new PreviewHost(type, dependencySource, takeProbePreview), time_before_load, time_before_unload)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                Add(new OsuSpriteText
                {
                    Text = type.Name,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = OsuFont.Default.With(size: 12),
                });
            }

            private Drawable? takeProbePreview()
            {
                Drawable? preview = probePreview;
                probePreview = null;
                return preview;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                // A card disposed before ever being shown still holds the instance created for the
                // selection check.
                if (isDisposing)
                    probePreview?.Dispose();
            }
        }

        /// <summary>
        /// Hosts the live component preview and scales it to fit. One instance per materialisation: the
        /// wrapper disposes it, and with it the component, once the card leaves the screen.
        /// </summary>
        private partial class PreviewHost : Container
        {
            private Drawable? component;

            private readonly CompositeDrawable? dependencySource;

            public PreviewHost(Type type, CompositeDrawable? dependencySource, Func<Drawable?> takeProbePreview)
            {
                RelativeSizeAxes = Axes.Both;

                this.dependencySource = dependencySource;
                component = takeProbePreview() ?? createInstance(type);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (component == null)
                    return;

                // 缺依赖的预览在加载时抛错。DelayedLoadUnloadWrapper 会把没接住的异常当成进程崩溃。
                try
                {
                    component.Anchor = Anchor.Centre;
                    component.Origin = Anchor.Centre;

                    Child = new DependencyBorrowingContainer(component, dependencySource)
                    {
                        RelativeSizeAxes = Axes.Both,
                    };
                }
                catch (Exception e)
                {
                    Logger.Log($"Ez skin component {component.GetType().Name} is not available in this editor scene ({e.GetType().Name}).");

                    if (component.Parent != null)
                        Clear(false);
                    else
                        component.Dispose();

                    component = null;
                }
            }

            private static Drawable? createInstance(Type type)
            {
                try
                {
                    return (Drawable)Activator.CreateInstance(type)!;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Ez skin component {type} could not be previewed in the library");
                    return null;
                }
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (component == null)
                    return;

                // A component with a zero-size axis (not yet laid out, or scrolled out of view and
                // culled) would otherwise divide to infinity. Guard every axis, not just the vector.
                if (!float.IsFinite(DrawWidth) || !float.IsFinite(DrawHeight)
                    || !float.IsFinite(component.DrawWidth) || !float.IsFinite(component.DrawHeight)
                    || DrawWidth <= 0 || DrawHeight <= 0
                    || component.DrawWidth <= 0 || component.DrawHeight <= 0)
                {
                    return;
                }

                float bestScale = Math.Min(DrawWidth / component.DrawWidth, DrawHeight / component.DrawHeight);

                if (float.IsFinite(bestScale) && bestScale > 0)
                    component.Scale = new Vector2(bestScale);
            }
        }

        /// <summary>
        /// Hosts a component preview using another container's dependency container, so components
        /// needing screen-scoped dependencies (score processor, etc.) still instantiate for preview.
        /// </summary>
        private partial class DependencyBorrowingContainer : Container
        {
            private readonly Drawable preview;

            private readonly CompositeDrawable? donor;

            public DependencyBorrowingContainer(Drawable preview, CompositeDrawable? donor)
            {
                this.preview = preview;
                this.donor = donor;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                try
                {
                    LoadComponent(preview);
                }
                catch (Exception e) when (SkinEditorPreviewDependencies.IsUnplaceable(e))
                {
                    preview.Dispose();
                    return;
                }

                Child = preview;
            }

            protected override bool ShouldBeConsideredForInput(Drawable child) => false;

            public override bool PropagateNonPositionalInputSubTree => false;

            public override bool PropagatePositionalInputSubTree => false;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var baseDependencies = base.CreateChildDependencies(parent);
                var source = donor?.Dependencies ?? baseDependencies;
                var dependencies = new DependencyContainer(source);
                SkinEditorPreviewDependencies.ApplyFallbacks(dependencies, source);
                return dependencies;
            }
        }
    }
}
