using UnityEngine;
using UnityEngine.UI;
using AkanekoLib;
using UnityEngine.Events;


public enum DailyButtonState
{
    Disable,
    Select,
    Clear,
    Locked,
    Today,
    NeedReward
}
public class DailyButton : MonoBehaviour
{
    public int _day;
    public Text _dayText;
    public Image _iconImage;
    public DailyButtonState _state;
    public GameObject _clearMark;
    public GameObject _selectFlame;
    public Color32 _selectColor;
    public Color32 _todayColor;
    public Color32 _lockedColor;
    public Color32 _clearColor;
    public Color32 _needRewardColor;
    public CustomButton _button;

    public event System.Action<int> onClickDailyButton;

    private void Awake() {
        _button.onClick += () =>
        {
            onClickDailyButton?.Invoke(_day);
        };
    }

    public void UpdateView(int day, int today, int lastDay, bool isSelected)
    {
         if(day <= 0 || day > lastDay)
        {
            _state = DailyButtonState.Disable;
            _iconImage.gameObject.SetActive(false);
            _button.IsEnable = false;
            _dayText.gameObject.SetActive(false);
            return;
        }
        bool isClear = false; // 仮
        // クリア判定：セーブデータ参照
        // 2進数で、日付に対応するビットが立っているかどうかで判定
        int clearSaveData = PlayerPrefs.GetInt("DailyClearData", 0);
        if ((clearSaveData & (1 << (day - 1))) != 0)
        {
            isClear = true;
        }

        _day = day;
        _dayText.text = day.ToString();
        // その日はクリア済みか？
        if(isClear)
        {
            _dayText.gameObject.SetActive(false);
            _selectFlame.SetActive(false);
            _clearMark.SetActive(true);
            _state = DailyButtonState.Clear;
            _iconImage.color = _clearColor;
            _button.IsEnable = false;
        }
        // 選択中か？
        else if(isSelected)
        {
            _state = DailyButtonState.Select;
            _iconImage.color = _selectColor;

            _selectFlame.SetActive(true);
            _clearMark.SetActive(false);
        }
        // それ以外
        else
        {
            _selectFlame.SetActive(false);
            _clearMark.SetActive(false);

            // 今日以前か？
            if (day < today)
            {
                _state = DailyButtonState.NeedReward;
                _iconImage.color = _needRewardColor;
            }
            // 今日か？
            else if (day == today)
            {
                _state = DailyButtonState.Today;
                _iconImage.color = _todayColor;
            }
            // 未来日か？
            else
            {
                _state = DailyButtonState.Locked;
                _iconImage.color = _lockedColor;
                _button.IsEnable = false;
            }
        }
    }

    public void SaveClear()
    {
        int clearSaveData = PlayerPrefs.GetInt("DailyClearData", 0);
        clearSaveData |= (1 << (_day - 1));
        PlayerPrefs.SetInt("DailyClearData", clearSaveData);
    }
}
