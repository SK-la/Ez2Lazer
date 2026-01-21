// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning.Scripting;
using osu.Game.Utils;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Handles the storage and retrieval of <see cref="Skin"/>s.
    /// </summary>
    /// <remarks>
    /// This is also exposed and cached as <see cref="ISkinSource"/> to allow for any component to potentially have skinning support.
    /// For gameplay components, see <see cref="RulesetSkinProvidingContainer"/> which adds extra legacy and toggle logic that may affect the lookup process.
    /// </remarks>
    public class SkinManager : ModelManager<SkinInfo>, ISkinSource, IStorageResourceProvider, IModelImporter<SkinInfo>
    {
        /// <summary>
        /// The default "classic" skin.
        /// </summary>
        public Skin DefaultClassicSkin { get; }

        private readonly AudioManager audio;

        private readonly Scheduler scheduler;

        private readonly GameHost host;

        private readonly IResourceStore<byte[]> resources;

        public readonly Bindable<Skin> CurrentSkin = new Bindable<Skin>();

        public readonly Bindable<Live<SkinInfo>> CurrentSkinInfo = new Bindable<Live<SkinInfo>>(ArgonSkin.CreateInfo().ToLiveUnmanaged());

        private readonly SkinImporter skinImporter;

        private readonly LegacySkinExporter skinExporter;

        private readonly IResourceStore<byte[]> userFiles;

        private Skin argonSkin { get; }

        private Skin trianglesSkin { get; }

        private SkinScriptManager scriptManager;

        public override bool PauseImports
        {
            get => base.PauseImports;
            set
            {
                base.PauseImports = value;
                skinImporter.PauseImports = value;
            }
        }

        public SkinManager(Storage storage, RealmAccess realm, GameHost host, IResourceStore<byte[]> resources, AudioManager audio, Scheduler scheduler)
            : base(storage, realm)
        {
            this.audio = audio;
            this.scheduler = scheduler;
            this.host = host;
            this.resources = resources;

            userFiles = new StorageBackedResourceStore(storage.GetStorageForDirectory("files"));

            skinImporter = new SkinImporter(storage, realm, this)
            {
                PostNotification = obj => PostNotification?.Invoke(obj),
            };

            var defaultSkins = new[]
            {
                DefaultClassicSkin = new DefaultLegacySkin(this),
                trianglesSkin = new TrianglesSkin(this),
                argonSkin = new ArgonSkin(this),
                new ArgonProSkin(this),
                new Ez2Skin(this),
                new SbISkin(this),
            };

            skinExporter = new LegacySkinExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };

            // Initialize the script manager
            scriptManager = new SkinScriptManager();
            realm.RegisterCustomObject(scriptManager);

            CurrentSkinInfo.ValueChanged += skin =>
            {
                CurrentSkin.Value = getSkin(skin.NewValue);
                CurrentSkinInfoChanged?.Invoke();
            };

            try
            {
                // Start with non-user skins to ensure they are present.
                foreach (var skin in defaultSkins)
                {
                    if (skin.SkinInfo.ID != SkinInfo.ARGON_SKIN && skin.SkinInfo.ID != SkinInfo.TRIANGLES_SKIN)
                        continue;

                    // if the user has a modified copy of the default, use it instead.
                    var existingSkin = realm.Run(r => r.All<SkinInfo>().FirstOrDefault(s => s.ID == skin.SkinInfo.ID));

                    if (existingSkin != null)
                        continue;

                    realm.Write(r =>
                    {
                        skin.SkinInfo.Protected = true;
                        r.Add(new SkinInfo
                        {
                            ID = skin.SkinInfo.ID,
                            Name = skin.SkinInfo.Name,
                            Creator = skin.SkinInfo.Creator,
                            Protected = true,
                            InstantiationInfo = skin.SkinInfo.InstantiationInfo
                        });
                    });
                }
            }
            catch
            {
                // May fail due to incomplete or breaking migrations.
            }
        }

        /// <summary>
        /// Returns a list of all usable <see cref="SkinInfo"/>s. Includes the special default and random skins.
        /// </summary>
        /// <returns>A list of available <see cref="SkinInfo"/>s.</returns>
        public List<Live<SkinInfo>> GetAllUsableSkins()
        {
            return Realm.Run(r =>
            {
                // First display all skins.
                var instances = r.All<SkinInfo>()
                                 .Where(s => !s.DeletePending)
                                 .OrderBy(s => s.Protected)
                                 .ThenBy(s => s.Name)
                                 .ToList();

                // Then add all default skin entries.
                var defaultSkins = r.All<SkinInfo>()
                                    .Where(s => s.ID == SkinInfo.ARGON_SKIN || s.ID == SkinInfo.TRIANGLES_SKIN)
                                    .ToList();

                foreach (var s in defaultSkins)
                    instances.Insert(instances.FindIndex(s2 => s2.Protected) + defaultSkins.IndexOf(s), s);

                return instances.Distinct().Select(s => r.Find<SkinInfo>(s.ID)?.ToLive(Realm.Realm)).Where(s => s != null).ToList();
            });
        }

        public event Action CurrentSkinInfoChanged;

        public Skin CurrentSkinInfo { get; private set; }

        public void RefreshCurrentSkin() => CurrentSkinInfo.TriggerChange();

        private Skin getSkin(Live<SkinInfo> skinInfo)
        {
            if (skinInfo == null)
                return null;

            Skin skin;

            try
            {
                switch (skinInfo.ID)
                {
                    case SkinInfo.ARGON_SKIN:
                        skin = argonSkin;
                        break;

                    case SkinInfo.TRIANGLES_SKIN:
                        skin = trianglesSkin;
                        break;

                    default:
                        skin = skinInfo.CreateInstance(this);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unable to load skin \"{skinInfo.ToString()}\"");
                return DefaultClassicSkin;
            }

            return skin;
        }

        public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => GetDrawableComponent(CurrentSkin.Value, lookup);

        public Drawable GetDrawableComponent(ISkin skin, ISkinComponentLookup lookup)
        {
            return skin?.GetDrawableComponent(lookup);
        }

        public ISample GetSample(ISampleInfo sampleInfo) => GetSample(CurrentSkin.Value, sampleInfo);

        public ISample GetSample(ISkin skin, ISampleInfo sampleInfo)
        {
            IEnumerable<Skin.LookupDebugType> lookupDebug = null;

            if (DebugDisplay.Value || (lookupDebug ??= GetIpcData<List<Skin.LookupDebugType>>("Debug:SkinLookupTypes"))?.Contains(Skin.LookupDebugType.None) != true)
                Skin.LogLookupDebug(this, sampleInfo, Skin.LookupDebugType.Enter);

            try
            {
                var sample = skin?.GetSample(sampleInfo);

                if (sample != null)
                    return sample;

                foreach (var skSource in AllSources)
                {
                    sample = skSource.GetSample(sampleInfo);
                    if (sample != null)
                        return sample;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, sampleInfo, Skin.LookupDebugType.Exit);
            }
        }

        public Texture GetTexture(string componentName) => GetTexture(CurrentSkin.Value, componentName);

        public Texture GetTexture(ISkin skin, string componentName)
        {
            Skin.LogLookupDebug(this, componentName, Skin.LookupDebugType.Enter);

            try
            {
                var texture = skin?.GetTexture(componentName);

                if (texture != null)
                    return texture;

                foreach (var skSource in AllSources)
                {
                    texture = skSource.GetTexture(componentName);
                    if (texture != null)
                        return texture;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, componentName, Skin.LookupDebugType.Exit);
            }
        }

        public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => GetConfig<TLookup, TValue>(CurrentSkin.Value, lookup);

        public IBindable<TValue> GetConfig<TLookup, TValue>(ISkin skin, TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Enter);

            try
            {
                var bindable = skin?.GetConfig<TLookup, TValue>(lookup);

                if (bindable != null)
                    return bindable;

                foreach (var source in AllSources)
                {
                    bindable = source.GetConfig<TLookup, TValue>(lookup);
                    if (bindable != null)
                        return bindable;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Exit);
            }
        }

        public IEnumerable<ISkin> AllSources
        {
            get
            {
                yield return DefaultClassicSkin;
            }
        }

        public IEnumerable<TValue> GetAllConfigs<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            var sources = new List<ISkin>();
            var items = new List<TValue>();

            addFromSource(CurrentSkin.Value, this);

            // This is not sane.
            foreach (var s in AllSources)
                addFromSource(s, default);

            return items;

            void addFromSource(ISkin source, object lookupFunction)
            {
                if (source == null) return;

                if (sources.Contains(source)) return;

                sources.Add(source);

                if (lookupFunction != null)
                    Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Enter);

                try
                {
                    // check for direct value
                    if (source.GetConfig<TLookup, TValue>(lookup)?.Value is TValue val)
                        items.Add(val);
                }
                finally
                {
                    if (lookupFunction != null)
                        Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Exit);
                }
            }
        }

        public IBindable<TValue> FindConfig<TLookup, TValue>(params Func<ISkin, IBindable<TValue>>[] lookups)
            where TValue : notnull
        {
            Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Enter);

            try
            {
                return FindConfig(CurrentSkin.Value, lookups) ?? FindConfig(AllSources, lookups);
            }
            finally
            {
                Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Exit);
            }
        }

        public IBindable<TValue> FindConfig<TLookup, TValue>(ISkin skin, params Func<ISkin, IBindable<TValue>>[] lookups)
            where TValue : notnull
        {
            Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Enter);

            try
            {
                if (skin == null)
                    return null;

                foreach (var l in lookups)
                {
                    var bindable = l(skin);

                    if (bindable != null)
                        return bindable;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Exit);
            }
        }

        public IBindable<TValue> FindConfig<TLookup, TValue>(IEnumerable<ISkin> allSources, params Func<ISkin, IBindable<TValue>>[] lookups)
            where TValue : notnull
        {
            Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Enter);

            try
            {
                foreach (var source in allSources)
                {
                    var bindable = FindConfig(source, lookups);

                    if (bindable != null)
                        return bindable;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, lookups, Skin.LookupDebugType.Exit);
            }
        }

        #region IResourceStorageProvider

        IRenderer IStorageResourceProvider.Renderer => host.Renderer;
        AudioManager IStorageResourceProvider.AudioManager => audio;
        IResourceStore<byte[]> IStorageResourceProvider.Resources => resources;
        IResourceStore<byte[]> IStorageResourceProvider.Files => userFiles;
        RealmAccess IStorageResourceProvider.RealmAccess => Realm;
        IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host.CreateTextureLoaderStore(underlyingStore);

        #endregion

        #region Implementation of IModelImporter<SkinInfo>

        public Action<IEnumerable<Live<SkinInfo>>> PresentImport
        {
            set => skinImporter.PresentImport = value;
        }

        public Task Import(params string[] paths) => skinImporter.Import(paths);

        public Task Import(ImportTask[] imports, ImportParameters parameters = default) => skinImporter.Import(imports, parameters);

        public IEnumerable<string> HandledExtensions => skinImporter.HandledExtensions;

        public Task<IEnumerable<Live<SkinInfo>>> Import(ProgressNotification notification, ImportTask[] tasks, ImportParameters parameters = default) =>
            skinImporter.Import(notification, tasks, parameters);

        public Task<Live<SkinInfo>> ImportAsUpdate(ProgressNotification notification, ImportTask task, SkinInfo original) =>
            skinImporter.ImportAsUpdate(notification, task, original);

        public Task<ExternalEditOperation<SkinInfo>> BeginExternalEditing(SkinInfo model) => skinImporter.BeginExternalEditing(model);

        public Task<Live<SkinInfo>> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default) =>
            skinImporter.Import(task, parameters, cancellationToken);

        public Task ExportCurrentSkin() => ExportSkin(CurrentSkinInfo.Value);

        public Task ExportSkin(Live<SkinInfo> skin) => skinExporter.ExportAsync(skin);

        #endregion

        public void Delete([CanBeNull] Expression<Func<SkinInfo, bool>> filter = null, bool silent = false)
        {
            Realm.Run(r =>
            {
                var items = r.All<SkinInfo>()
                             .Where(s => !s.Protected && !s.DeletePending);
                if (filter != null)
                    items = items.Where(filter);

                // check the removed skin is not the current user choice. if it is, switch back to default.
                Guid currentUserSkin = CurrentSkinInfo.Value.ID;

                if (items.Any(s => s.ID == currentUserSkin))
                    scheduler.Add(() => CurrentSkinInfo.Value = ArgonSkin.CreateInfo().ToLiveUnmanaged());

                Delete(items.ToList(), silent);
            });
        }

        public void SetSkinFromConfiguration(string guidString)
        {
            Live<SkinInfo> skinInfo = null;

            if (Guid.TryParse(guidString, out var guid))
                skinInfo = Query(s => s.ID == guid);

            if (skinInfo == null)
            {
                if (guid == SkinInfo.CLASSIC_SKIN)
                    skinInfo = DefaultClassicSkin.SkinInfo;
            }

            CurrentSkinInfo.Value = skinInfo ?? trianglesSkin.SkinInfo;
        }
    }
}
