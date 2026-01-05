using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class DebugAddressableImageLoader
{
    // シーン上の非アクティブ含むすべての AddressableImageLoader コンポーネントを検索し、以下のいずれかを行う
    // ・LoadExternal を呼び出す
    // ・同オブジェクトのImageをnullにする
    [MenuItem("Tools/Debug AddressableImageLoad/Load External Images")]
    public static void LoadExternalImages()
    {
        var loaders = GameObject.FindObjectsOfType<AddressableImageLoader>(true);
        EditorUtility.ClearProgressBar();
        var total = (float)loaders.Length;
        var count = 0f;
        foreach (var loader in loaders)
        {
            // 進捗バー表示
            EditorUtility.DisplayProgressBar("Addressable Image Loading", $"Loading Image for {loader.gameObject.name}", count / total);
            // ロード実行
            loader.LoadExternal();
            count += 1f;
        }
        EditorUtility.ClearProgressBar();
    }

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