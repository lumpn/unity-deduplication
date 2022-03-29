using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Lumpn.Deduplication
{
    public sealed class GroupDuplicateDependenciesRule : AnalyzeRule
    {
        private sealed class CheckBundleDupeDependenciesBridge : CheckBundleDupeDependencies
        {
            public Dictionary<GUID, List<string>> GetImplicitAssets()
            {
                return GetImplicitGuidToFilesMap();
            }

            public bool IsValidAsset(string path)
            {
                return IsValidPath(path);
            }
        }

        private const string _desiredGroupName = "Grouped Duplicate Asset Isolation";

        private readonly Dictionary<string, string> _bundleSlugs = new Dictionary<string, string>();

        private readonly CheckBundleDupeDependenciesBridge _bridge = new CheckBundleDupeDependenciesBridge();

        public override string ruleName { get { return "Group Duplicate Dependencies"; } }

        public override bool CanFix { get { return true; } }

        public override void ClearAnalysis()
        {
            _bridge.ClearAnalysis();
        }

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            return _bridge.RefreshAnalysis(settings);
        }

        public override void FixIssues(AddressableAssetSettings settings)
        {
            _bridge.RefreshAnalysis(settings);

            var group = GetOrCreateDeduplicationGroup(settings);

            var implicitEntries = _bridge.GetImplicitAssets();
            foreach (var entry in implicitEntries)
            {
                // guid of the implicitly referenced asset
                var guid = entry.Key;

                // bundles that impliclitly reference the asset
                var bundles = entry.Value;
                if (bundles.Distinct().Count() < 2)
                {
                    // implicit asset not duplicated
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!_bridge.IsValidAsset(path))
                {
                    // not a valid asset
                    continue;
                }

                var assetEntry = settings.CreateOrMoveEntry(guid.ToString(), group, false, false);
                foreach (var bundle in bundles)
                {
                    var slug = GetOrCreateSlug(bundle);
                    assetEntry.SetLabel(slug, true, true, false);
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        }

        private AddressableAssetGroup GetOrCreateDeduplicationGroup(AddressableAssetSettings settings)
        {
            var existingGroup = settings.FindGroup(_desiredGroupName);
            if (existingGroup)
            {
                return existingGroup;
            }

            var group = settings.CreateGroup(_desiredGroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            bundleSchema.IncludeAddressInCatalog = false;
            bundleSchema.IncludeGUIDInCatalog = false;
            bundleSchema.IncludeLabelsInCatalog = false;
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;

            var updateSchema = group.GetSchema<ContentUpdateGroupSchema>();
            updateSchema.StaticContent = true;

            return group;
        }

        private string GetOrCreateSlug(string bundleIdentifier)
        {
            if (!_bundleSlugs.TryGetValue(bundleIdentifier, out string slug))
            {
                slug = $"B{_bundleSlugs.Count}";
                _bundleSlugs.Add(bundleIdentifier, slug);
            }
            return slug;
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            AnalyzeSystem.RegisterNewRule<GroupDuplicateDependenciesRule>();
        }
    }
}
