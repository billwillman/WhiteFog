using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class DemoUi : MonoBehaviour
    {
        public static DemoUi instance;
        public Animation LevelUpFullScreen;
        public Text LevelNum;
        public Text DifficultyText;
        public CanvasGroup LootInfoPanel;
        public Text LootInfoText;
        public GameObject StatItem;
        public AudioClip buffSound;
       
        public GameObject GameOver;
        public GameObject GameOverTip;
        public AudioClip GameOverSound;
        public AudioClip GameStartSound;
        public AudioSource BGM;
        
        public CanvasGroup LootedPanel;
        public Text LootedText;
        private float LootTimer = 0F;
        private float LootedTimer = 0F;
        private void Awake()
        {
            instance = this;
        }

        public void ToggleGameOver(bool _gameover)
        {
            GameOver.GetComponent<Animation>().Stop();
            if (_gameover)
            {
                BGM.Stop();
                GameManager.PlaySound2D(GameOverSound, 1F);
            }
            else
            {
                BGM.Play();
                GameManager.PlaySound2D(GameStartSound, 1F);
            }
            GameOver.SetActive(_gameover);
            GameOverTip.SetActive(false);
        }

        private void Start()
        {
            foreach (var obj in GameManager.GetPlayer().Attributes) {
                if (!GameManager.GetAttribute(obj.uid).stringValue && GameManager.GetAttribute(obj.uid).coreStats)
                {
                    int _id = GameManager.GetAttribute(obj.uid).id;
                    GameObject _newStat = Instantiate(StatItem, StatItem.transform.parent);
                    _newStat.GetComponent<StatsUi>().Init(GameManager.GetPlayer(), _id);
                    _newStat.SetActive(true);
                }
            }
        }

        public void PlayLevelUpFullScreen(int _level)
        {
            LevelNum.text = _level.ToString();
            LevelUpFullScreen.Stop();
            LevelUpFullScreen.Play();
        }

        private void Update()
        {
            DifficultyText.text = "Difficulty Level: " + Mathf.FloorToInt(DemoManager.instance.Level).ToString();
            if (LootTimer > 0F)
            {
                LootTimer -= Time.deltaTime*2F;
                LootInfoPanel.alpha = Mathf.Min(LootTimer, 1F);
                if (!LootInfoPanel.gameObject.activeSelf) LootInfoPanel.gameObject.SetActive(true);
            }
            else
            {
                HideLootInfo();
            }

            if (LootedTimer > 0F)
            {
                LootedTimer -= Time.deltaTime * 2F;
                LootedPanel.alpha = Mathf.Min(LootedTimer, 1F);
                if (!LootedPanel.gameObject.activeSelf) LootedPanel.gameObject.SetActive(true);
            }
            else
            {
                HideLootedMsg();
            }
            if (GameOver.activeSelf)
            {
                GameOverTip.SetActive(GameManager.GetPlayerInstance()==null);
            }
        }

        public void ShowLootInfo(string _text)
        {
            LootInfoText.text = _text;
            LootInfoPanel.alpha = 1F;
            LootInfoPanel.gameObject.SetActive(true);
            LootTimer = 1.2F;
        }

        public void HideLootInfo()
        {
            LootTimer = 0F;
            LootInfoPanel.alpha = 0F;
            if(LootInfoPanel.gameObject.activeSelf) LootInfoPanel.gameObject.SetActive(false);
        }

        public void ShowLootedMsg(string _text)
        {
            LootedText.text = _text;
            LootedPanel.alpha = 1F;
            LootedPanel.gameObject.SetActive(true);
            LootedPanel.GetComponent<Animation>().Stop();
            LootedPanel.GetComponent<Animation>().Play();
            LootedTimer = 5F;
            GameManager.PlaySound2D(buffSound, 1F);
        }

        public void HideLootedMsg()
        {
            LootedTimer = 0F;
            LootedPanel.alpha = 0F;
            if (LootedPanel.gameObject.activeSelf) LootedPanel.gameObject.SetActive(false);
        }

    }
}
