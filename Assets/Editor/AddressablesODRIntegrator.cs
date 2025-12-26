#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesODRIntegrator
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject proj = new PBXProject();
        proj.ReadFromString(File.ReadAllText(projPath));

        // --- 修正：ターゲット取得を2つにする ---
        string mainTargetGuid = proj.GetUnityMainTargetGuid();
        string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid(); // フレームワーク側も取得

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        // 1. 両方のターゲットでODRを有効化
        proj.SetBuildProperty(mainTargetGuid, "ENABLE_ON_DEMAND_RESOURCES", "YES");
        proj.SetBuildProperty(mainTargetGuid, "EMBED_ASSET_PACKS_IN_PRODUCT_BUNDLE", "NO");
        // proj.SetBuildProperty(frameworkTargetGuid, "ENABLE_ON_DEMAND_RESOURCES", "NO"); // 必要ならNOと明示
        
        // 2. パス設定（スクショの階層に正確に合わせる）
        string sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "Library/com.unity.addressables/aa/iOS/iOS");
        string destDirInXcode = "Data/Raw/com.unity.addressables/iOS";
        string absoluteDestDir = Path.Combine(pathToBuiltProject, destDirInXcode);

        if (!Directory.Exists(sourceDir)) {
            Debug.LogError($"[ODR] Source directory not found: {sourceDir}");
            return;
        }
        if (!Directory.Exists(absoluteDestDir)) Directory.CreateDirectory(absoluteDestDir);

        int processedCount = 0;

        // 3. 全グループを走査してファイルを登録
        foreach (var group in settings.groups)
        {
            if (group == null || group.IsDefaultGroup()) continue;

            var odrSchema = group.GetSchema<AppleODRSchema>();
            if (odrSchema != null && !string.IsNullOrEmpty(odrSchema.odrTag))
            {
                // グループ名に一致する実際のbundleファイルを検索 (ハッシュ対応)
                string searchPattern = $"{group.Name}_*.bundle";
                string[] files = Directory.GetFiles(sourceDir, searchPattern);

                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destPath = Path.Combine(absoluteDestDir, fileName);
                    
                    // A. 物理ファイルをコピー
                    File.Copy(file, destPath, true);

                    // --- 修正後のBとCの工程 ---
                    // B. Xcodeにファイルを認識させる
                    string fileInProj = Path.Combine(destDirInXcode, fileName);
                    string fileGuid = proj.FindFileGuidByProjectPath(fileInProj);
                    if (string.IsNullOrEmpty(fileGuid))
                    {
                        fileGuid = proj.AddFile(fileInProj, fileInProj, PBXSourceTree.Source);
                    }

                    // C. タグの登録
                    // 1. ファイルをターゲットの「ビルド対象」に追加（戻り値を受け取らない形に修正）
                    proj.AddFileToBuild(mainTargetGuid, fileGuid);

                    // 2. タグを付与
                    proj.AddAssetTagForFile(mainTargetGuid, fileGuid, odrSchema.odrTag);
                    
                    processedCount++;
                }
            }
        }

        File.WriteAllText(projPath, proj.WriteToString());
        Debug.Log($"✅ ODR Integration Complete: {processedCount} bundles tagged.");
    }
}
#endif