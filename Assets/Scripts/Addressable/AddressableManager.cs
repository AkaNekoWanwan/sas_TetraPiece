using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public static class AddressableManager
{
    // private static List<GameObject> loadedObjects = new List<GameObject>();
    public static async Task<StageInfo> InstantiateStageAsync(int currentStage, int MAxStageCount, string stagePrefabsPath, bool isDailyStage = false)
    {
        // ステージ番号は0うめ３けたで管理しているため、引数で受け取ったステージ番号を加工する

        int stageINdex = currentStage % MAxStageCount;
        string stageName = isDailyStage ? $"DailyStage{stageINdex + 1:D3}" : $"Stage{stageINdex + 1:D3}";
        var address = $"{stagePrefabsPath}{stageName}.prefab";
        var handle = Addressables.InstantiateAsync(address);
        await handle.Task;
        if(handle.Status == AsyncOperationStatus.Succeeded)
        {
            var stageObject = handle.Result;
            // Ondestroyで解放させる
            stageObject.AddComponent<AddressableDestroyer>();
            // loadedObjects.Add(stageObject);
            return stageObject.GetComponent<StageInfo>();
        }
        else
        {
            Debug.LogError($"Failed to instantiate stage at address: {address}");
            return null;
        }
    }
    

    public static async Task<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
    {
        var handle = Addressables.LoadAssetAsync<T>(address);
        await handle.Task;
        if(handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result;
        }
        else
        {
            Debug.LogError($"Failed to load asset at address: {address}");
            return null;
        }
    }

}