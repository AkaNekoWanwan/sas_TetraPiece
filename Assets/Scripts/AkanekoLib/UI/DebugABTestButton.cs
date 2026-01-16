using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class DebugABTestButton : MonoBehaviour
{
    // ---------- 定数宣言 ----------
    // ---------- ゲームオブジェクト参照変数宣言 ----------
    // ---------- プレハブ ------------
    // ---------- プロパティ ----------
    [SerializeField, Tooltip("ボタン：パラメータ変更")] private Button _buttonChangeParam = default;
    [SerializeField, Tooltip("テキスト：プロパティ名")] private Text _textPropertyName = default;
    [SerializeField, Tooltip("テキスト：ボタンに表示するパラメータ")] private Text _textButtonNum = default;
    private string _propertyName = "";
    private List<int> _propertyList;
    // ---------- クラス変数宣言 ----------
    // ---------- インスタンス変数宣言 -----
    // ---------- Unity組込関数 ----------
    // ---------- Public関数 ----------
    public void Initialize(string propertyName, List<int> propertyList, UnityAction onClick = null)
    {
        _propertyName = propertyName;
        _propertyList = propertyList; 

        ResetView();

        // ボタンを押した時の処理設定：プロパティを次の値に設定し直す
        _buttonChangeParam.onClick.AddListener(()=>
        {
            int currentParam = PlayerPrefs.GetInt(_propertyName, -1);
            currentParam ++;
            currentParam %= _propertyList.Count;

            // 再設定した値をセーブデータ保存 & Firebaseに再登録
            PlayerPrefs.SetInt(_propertyName, currentParam);
            Firebase.Analytics.FirebaseAnalytics.SetUserProperty(_propertyName,PlayerPrefs.GetInt(_propertyName).ToString());
            
            Debug.Log("ユーザープロパティ再設定！:" + _propertyName + ", " + currentParam);
            ResetView();

            // その他、デバッグビューの更新などの、設定されたコールバック実行
            onClick();
        });
    }
    // ---------- Private関数 ----------
    private void ResetView(){
        int currentParam = PlayerPrefs.GetInt(_propertyName, -1);
        if( currentParam == -1 )
            Debug.LogError("なんかプロパティが-1なんだけどおおお!?:" + _propertyName);

        string status = UserSegment.instance.GetValue<string>(_propertyName, currentParam);

        // ボタンの数字更新
        _textButtonNum.text = currentParam.ToString();
        // パラメータ名更新
        _textPropertyName.text = _propertyName + " : " + status;
    }
}
