#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class ScreenshotTool
{
    private const string FolderPath = "Screenshots";

    // Ctrl + 1 (Windows) または Cmd + 1 (macOS) でスクリーンショットを撮影
    // "%"はCtrl (Cmd)、"&"はAlt、"#"はShiftを表します
    [MenuItem("Tools/Screenshot/Take Screenshot (1x) &S", false, 1)]
    private static void TakeScreenshot()
    {
        CaptureScreenshot(1);
    }

    [MenuItem("Tools/Screenshot/Take Screenshot (2x) &2", false, 2)]
    private static void TakeScreenshot2x()
    {
        CaptureScreenshot(2);
    }

    [MenuItem("Tools/Screenshot/Take Screenshot (4x) &4", false, 3)]
    private static void TakeScreenshot4x()
    {
        CaptureScreenshot(4);
    }

    private static void CaptureScreenshot(int scale)
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
            Debug.Log($"フォルダ '{FolderPath}' を作成しました。");
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filePath = $"{FolderPath}/screenshot_{timestamp}_{scale}x.png";

        ScreenCapture.CaptureScreenshot(filePath, scale);
        Debug.Log($"スクリーンショットを撮影しました: {filePath}");

        AssetDatabase.Refresh();
    }
}
#endif