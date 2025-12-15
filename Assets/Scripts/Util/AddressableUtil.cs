using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public class AddressableUtil : IDisposable
{ 
    private static readonly HashSet<(string key, AsyncOperationHandle handle)> Handles = new ();

    /// <summary>
    /// Addressable全体の初期化.
    /// </summary>
    public static async UniTask InitAsync()
    {
        try
        {
            // Addressablesの初期化を待機
            var initHandle = Addressables.InitializeAsync();
            await initHandle.Task;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during Addressables initialization or asset loading: {ex}");
        }
    }

    /// <summary>
    /// アセットの読み込み.
    /// </summary>
    /// <param name="assetKey"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async UniTask<T> LoadAssetAsync<T>(string assetKey, CancellationToken cancellationToken)
        where T : Object
    {
        // ローカルのアセットをロード
        if (Handles.All(x => x.key != assetKey))
        {
            Handles.Add((assetKey, Addressables.LoadAssetAsync<T>(assetKey)));
        }
        var handleObj = Handles.FirstOrDefault(x => x.key == assetKey).handle;
        var typedHandle = handleObj.Convert<T>();

        // アセットのロード完了を待機
        var asset = await typedHandle.ToUniTask(cancellationToken: cancellationToken);
        return asset;
    }

    /// <summary>
    /// 読み込んだアセットハンドルの破棄.
    /// </summary>
    public void Dispose()
    {
        foreach (var handle in Handles)
        {
            Addressables.Release(handle.handle);
        }
        Handles.Clear();
    }
}
