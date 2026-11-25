using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class DemoSaveData
    {
        /// <summary>
        /// I used this class to serialize a json string and used it as arg of EntityManagerData.Save method, 
        /// so the following data can be save in the same file as Entities data.
        /// </summary>
        public float Level;
        public List<string> AliveEnemies = new List<string>();
    }
    public class DemoManager : MonoBehaviour
    {
        public static DemoManager instance;
        public GameObject PlayerPrefab;
        public CameraControl CameraScript;
        public GameObject EnemyPrefab;
        public GameObject [] LootPackPrefabs;
        public List<string> EnemyEntityList = new List<string>();
        public Transform[] SpawnPoints;
        public AudioClip LootSound;
        public float Level = 1F;//This is the difficulty level that increases as time goes on.
        string savePath;
        private bool GameOver = false;
        private GameObject Player;

        void Awake()
        {
            instance = this;
            savePath = Application.persistentDataPath + "/DemoSave.sav";
            Debug.Log("Save Path: "+savePath);
            Application.targetFrameRate = 60;
            SpawnPlayer();
            Load();//Load the game from save file
        }

        private void OnApplicationQuit()
        {
            Save();//Save the game when the game stop.
        }
        void Update()
        {
            Level += Time.deltaTime * 0.02F;
            if (Time.frameCount % 120 == 0)
            {
                foreach (var uid in EnemyEntityList)
                {
                    if (GameManager.GetEntityInstance(uid) == null) SpawnEnemy(uid, SpawnPoints[Random.Range(0, SpawnPoints.Length)].position, true);
                    //Spawn a new enemy if any of them gets killed.
                }
            }
            if (GameOver && Player==null && InputProxy.GetKeyDown(KeyCode.Space))
            {
                RestartGame();
            }
        }

        private void RestartGame()
        {
            if (File.Exists(savePath)) File.Delete(savePath); //Delete the save file
            GameManager.DeleteAllEntityInstance();
            Level = 1F;
            GameOver = false;
            GameManager.EntityManagerData.RuntimeInit();
            SpawnPlayer();
            DemoUi.instance.ToggleGameOver(false);
        }

        public void SpawnPlayer()
        {
            Player = Instantiate(PlayerPrefab, PlayerPrefab.transform.position, PlayerPrefab.transform.rotation);
            Player.GetComponent<PlayerControl>().mCamera = CameraScript;
            CameraScript.AssignPlayer(Player.transform);
        }

        public void Save()
        {
            if (GameOver)return;
            ///Collecting extra data for saving
            DemoSaveData _extraData = new DemoSaveData();
            _extraData.Level = Level;
            _extraData.AliveEnemies = new List<string>();
            foreach (var obj in GameManager.GetEntityInstanceList()) {
                if(obj.uid!="player" && obj.AvailableForInteraction) _extraData.AliveEnemies.Add(obj.uid);
            }
            GameManager.EntityManagerData.Save(savePath, JsonUtility.ToJson(_extraData)); //Save the game with the json string of extra data we just collected.
        }
 
        public void Load()
        {
            if (!File.Exists(savePath)) return;
            string _extrajson= GameManager.EntityManagerData.Load(savePath); //Load the game and retrieves the extra data we saved toghther with entities data.
            if (_extrajson != "")
            {
                DemoSaveData _extraData = (DemoSaveData)JsonUtility.FromJson(_extrajson, typeof(DemoSaveData)); //Convert the json string of the extra data
                Level = _extraData.Level; //Override level
                foreach (var obj in _extraData.AliveEnemies) //Spawn the same enemies in the last game.
                {
                    SpawnEnemy(obj, GameManager.EntityManagerData.GetEntity(obj).Position,false).SendMessage("LoadAnimation", SendMessageOptions.DontRequireReceiver);
                    //Send the LoadAnimation message to load their animation name and normalized time from their custom data in Entity class
                }
            }
        }

        public void DropRandomLoot(Vector3 _pos)
        {
            GameObject _newLoot = Instantiate(LootPackPrefabs[Random.Range(0, LootPackPrefabs.Length)], _pos,Quaternion.identity);
            GameManager.PlaySound2D(LootSound,1F);
        }


        public void ToggleGameOver()
        {
            DemoUi.instance.ToggleGameOver(true);
            if (File.Exists(savePath)) File.Delete(savePath); //Delete the save file
            GameOver = true;
        }


       


        private GameObject SpawnEnemy(string _uid,Vector3 _pos, bool _refillHp)
        {
            GameObject _newEnemy = Instantiate(EnemyPrefab);
            _newEnemy.GetComponent<EntityComponent>().uid = _uid; //Assign a Entity data to this enemy by setting the uid.
            GameManager.GetEntity(_uid).Position = _pos; //Override the position of the Entity data
            _newEnemy.transform.position = GameManager.GetEntity(_uid).Position;
            GameManager.GetEntity(_uid).SetAttributeValue("lvl", Mathf.FloorToInt(Level));//Override the level attribute of the Entity data
            GameManager.GetEntity(_uid).AttributesUpgradeLevel = Mathf.FloorToInt(Level);//Bind the level attribute with AttributesUpgradeLevel, so the stats gets boost.
            if (_refillHp) GameManager.GetEntity(_uid).SetAttributeValue("chp", GameManager.GetEntity(_uid).GetAttributeInt("hp"));//Refill hp
            GameManager.GetEntity(_uid).AvailableForInteraction = true;
            _newEnemy.GetComponent<EnemyControl>().Intelligence = Mathf.Lerp(0F, 1F, Level / 30F);
            _newEnemy.GetComponent<EnemyControl>().SightRange = Mathf.Lerp(10F, 30F, Level / 30F);
            _newEnemy.GetComponent<EnemyControl>().Exp = Mathf.FloorToInt(80F + Level * 20F);
            _newEnemy.gameObject.SetActive(true);
            return _newEnemy;
        }
    }
}
