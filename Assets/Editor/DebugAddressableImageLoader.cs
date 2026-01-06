using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class DebugAddressableImageLoader
{
    [MenuItem("Tools/Debug AddressableImageLoad/Load External Images")]
    public static void LoadExternalImages()
    {
        // EditorUtility.ClearProgressBar();
        // EditorUtility.DisplayProgressBar("Addressable Image Loading", $"Starting...", 0f);
        var loaders = GameObject.FindObjectsOfType<AddressableImageLoader>(true);
        int total = loaders.Length;
        Debug.Log($"[DebugAddressableImageLoader] Found {total} AddressableImageLoader components.");
        // try 
        // {
            for (int i = 0; i < total; i++)
            {
                // 10件ごとにプログレスバーを更新（負荷軽減）
                // かつ、キャンセルボタンが押されたかチェック
                // if (i % 10 == 0)
                // {
                //     float progress = (float)i / total;
                //     if (EditorUtility.DisplayCancelableProgressBar(
                //         "Addressable Image Loading", 
                //         $"Processing {i}/{total}: {loaders[i].gameObject.name}", 
                //         progress))
                //     {
                //         Debug.Log("User cancelled the operation.");
                //         break;
                //     }
                // }
                
                if (loaders[i] != null)
                {
                    Debug.Log($"[DebugAddressableImageLoader] Loading image for {loaders[i].gameObject.name} ({i + 1}/{total})");
                    loaders[i].LoadExternal();
                }
            }
        // }
        // finally 
        // {
        //     // try-finallyで囲むことで、エラーが起きても確実にバーを消す
        //     EditorUtility.ClearProgressBar();
        // }
    }

    // ClearImages も同様の構成にすることをお勧めします
    [MenuItem("Tools/Debug AddressableImageLoad/Clear Images")]
    public static void ClearImages()
    {
        var loaders = GameObject.FindObjectsOfType<AddressableImageLoader>(true);
        var total = (float)loaders.Length;
        var count = 0f;
        foreach (var loader in loaders)
        {
            // 進捗バー表示
            EditorUtility.DisplayProgressBar("Addressable Image Loading", $"Loading Image for {loader.gameObject.name}", count / total);
            var image = loader.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.sprite = null;
            }
            count += 1f;
        }
        EditorUtility.ClearProgressBar();
    }
}
#endif