using UnityEngine;
using System.Collections.Generic;

public static class SiblingFinder
{
    /// <summary>
    /// 現在のオブジェクトと同じ階層にある、指定した名前のオブジェクトを取得する。
    /// </summary>
    /// <param name="targetName">検索したい兄弟オブジェクトの名前</param>
    /// <returns>見つかったGameObject。見つからない場合はnull。</returns>
    public static GameObject FindSiblingByName(GameObject self, string targetName)
    {
        // 1. まず親オブジェクトのTransformを取得する
        Transform parentTransform = self.transform.parent;

        // 親がいない場合（ルート階層の場合）、兄弟は存在しないと見なす
        if (parentTransform == null)
        {
            Debug.LogWarning(self.name + "はルート階層にあります。兄弟オブジェクトは存在しません。");
            return null;
        }

        // 2. 親オブジェクトの子（＝兄弟オブジェクト全体）を全てチェックする
        foreach (Transform siblingTransform in parentTransform)
        {
            GameObject sibling = siblingTransform.gameObject;
            
            // 3. 自身（this.gameObject）は除外する
            if (sibling == self)
            {
                continue;
            }

            // 4. 名前が一致するかチェックする
            if (sibling.name == targetName)
            {
                // 見つかったら即座に返す
                return sibling;
            }
        }

        // 全ての子オブジェクトをチェックしたが、見つからなかった
        return null;
    }
    
}