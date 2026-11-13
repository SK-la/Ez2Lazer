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
using osu.Game.EzOsuGame.Layout;
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

            if (hudLayer == null)
                return;

            EzLayoutContainer? target = hudLayer.ActiveTarget;
            CompositeDrawable dependencySource = (CompositeDrawable?)target ?? hudLayer;

            try
            {
                Drawable instance = (Drawable)Activator.CreateInstance(type)!;
                var serialisable = (ISerialisableDrawable)instance;

                bool placeable = target == null
                    ? serialisable.CanBeSelected
                    : serialisable.CanBePlaced(target.Components);

                if (!placeable)
                {
                    instance.Dispose();
                    return;
                }

                list.Add(new ComponentPlaceButton(instance, dependencySource)
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
        /// Preview structure matches <see cref="SkinComponentToolbox.ToolboxComponentButton"/> so scaling
        /// and dependency injection behave like the official skin editor toolbox.
        /// </remarks>
        private partial class ComponentPlaceButton : EzButton
        {
            private const float thumbnail_height = 72;

            private readonly Drawable component;

            private readonly DependencyBorrowingContainer innerContainer;

            public ComponentPlaceButton(Drawable component, CompositeDrawable dependencySource)
            {
                this.component = component;

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
                    Child = innerContainer = new DependencyBorrowingContainer(component, dependencySource)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                });

                Add(new OsuSpriteText
                {
                    Text = component.GetType().Name,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = OsuFont.Default.With(size: 12),
                });

                component.Anchor = Anchor.Centre;
                component.Origin = Anchor.Centre;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (component.DrawSize != Vector2.Zero)
                {
                    float bestScale = Math.Min(
                        innerContainer.DrawWidth / component.DrawWidth,
                        innerContainer.DrawHeight / component.DrawHeight);

                    innerContainer.Scale = new Vector2(bestScale);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                // Parent dispose already releases a preview that was added as a child. Disposing it again
                // runs component Dispose before the base IsDisposed guard and cancels a disposed token.
                if (isDisposing && component.Parent == null)
                    component.Dispose();

                base.Dispose(isDisposing);
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
                if (donor == null)
                    return;

                // A preview that cannot resolve its dependencies must not fail the button load,
                // or one missing dependency removes the whole library entry.
                try
                {
                    LoadComponent(preview);
                }
                catch (Exception e) when (SkinEditorPreviewDependencies.IsUnplaceable(e))
                {
                    preview.Dispose();
                    return;
                }
                catch (Exception e)
                {
                    Logger.Log($"Ez skin component {preview.GetType().Name} preview could not be loaded ({e.GetType().Name}).");
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
                if (donor == null)
                    return baseDependencies;

                var dependencies = new DependencyContainer(donor.Dependencies);

                if (baseDependencies.TryGet<global::osu.Game.Overlays.SkinEditor.SkinEditor>(out var skinEditor))
                    dependencies.CacheAs(skinEditor);

                SkinEditorPreviewDependencies.ApplyFallbacks(dependencies, donor.Dependencies);

                return dependencies;
            }
        }
    }
}
