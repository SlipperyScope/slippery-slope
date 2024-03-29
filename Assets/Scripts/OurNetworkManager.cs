﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Generated;
using BeardedManStudios.Forge.Networking.Unity;
using UnityEngine;
using UnityEngine.UI;

class PathModel {
    public List<Vector3> path;
}

[System.Serializable]
public class LevelRecord
{
    public GameObject LevelPrefab;
    public float MaxInk;
    public int id;
}

public class OurNetworkManager : NetworkManagerBehavior
{
    public GameObject lineTemplate;
    public bool Debugging = false;
    public List<LevelRecord> levels;
    private LevelRecord curLevel;
    private GameObject curLevelObj;
    private int Attempts = 0;
    private Text levelText;
    private GameObject[] walls;

    IEnumerator Countdown()
    {
        yield return new WaitForSeconds(0.5f);
        this.SetLevel(1);
    }

    IEnumerator LevelWinCountdown()
    {
        yield return new WaitForSeconds(3.0f);
        this.NextLevel();
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Countdown());
        this.levelText = GameObject.Find("levelText").GetComponent<Text>();

        // If debugging isn't explicitly on in the editor, defer to the
        // single player flag
        if (!this.Debugging) {
            this.Debugging = GameManager.isSinglePlayer;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ResetLevel() {
        Debug.Log("RPC ResetLevel");
        if (this.Debugging) {
            this._resetLevel();
            this._setInkLevel(1);
            this._setAttempts(this.Attempts + 1);
        } else {
            this.networkObject.SendRpc(RPC_RESET_LEVEL, Receivers.All);
            this.networkObject.SendRpc(RPC_SET_INK_LEVEL, Receivers.All, 1.0f);
            this.networkObject.SendRpc(RPC_SET_ATTEMPTS, Receivers.All, this.Attempts + 1);
        }
    }

    public void AddLine(List<Vector3> path) {
        Debug.Log("RPC AddLine");
        // JSON encode path
        var model = new PathModel(){ path = path };
        var json = JsonUtility.ToJson(model);

        // send RPC
        if (this.Debugging) {
            this._sendPath(json);
        } else {
            Debug.Log($"Sending RPC... {json}");
            this.networkObject.SendRpc(RPC_SEND_PATH, Receivers.All, json);
        }
    }

    public void SetInkLevel(float level)
    {
        Debug.Log("RPC SetInkLevel");
        if (this.Debugging)
        {
            this._setInkLevel(level);
        } else
        {
            this.networkObject.SendRpc(RPC_SET_INK_LEVEL, Receivers.All, level);
        }

    }

    public void SetLevel(int levelId)
    {
        Debug.Log($"RPC SetLevel ({levelId})");
        if (this.Debugging)
        {
            this._resetLevel();
            this._setLevel(levelId);
            this._setInkLevel(1);
            this._setAttempts(0);
        } else
        {
            this.networkObject.SendRpc(RPC_RESET_LEVEL, Receivers.All);
            this.networkObject.SendRpc(RPC_SET_LEVEL, Receivers.All, levelId);
            this.networkObject.SendRpc(RPC_SET_INK_LEVEL, Receivers.All, 1.0f);
            this.networkObject.SendRpc(RPC_SET_ATTEMPTS, Receivers.All, 0);
        }
    }

    public void NextLevel()
    {
        if (curLevel != null)
        {
            this.SetLevel(curLevel.id + 1);
        }
    }

    public void Victory() {
        if (this.Debugging) {
            this._victory();
        } else {
            this.networkObject.SendRpc(RPC_VICTORY, Receivers.All);
        }
        StartCoroutine(LevelWinCountdown());
    }

    public override void resetLevel(RpcArgs args) {
        MainThreadManager.Run(() => {
            this._resetLevel();
        });
    }

    public override void sendPath(RpcArgs args) {
        MainThreadManager.Run(() => {
            var pathJson = args.GetNext<string>();
            Debug.Log($"Received RPC... {pathJson}");
            this._sendPath(pathJson);
        });
    }

    public override void victory(RpcArgs args) {
        MainThreadManager.Run(() => {
            this._victory();
        });
    }

    public override void setLevel(RpcArgs args) {
        MainThreadManager.Run(() =>
        {
            var levelId = args.GetNext<int>();
            this._setLevel(levelId);
        });
    }

    public override void setAttempts(RpcArgs args)
    {
        MainThreadManager.Run(() =>
        {
            var num = args.GetNext<int>();
            this._setAttempts(num);
        });
    }

    public override void setInkLevel(RpcArgs args) {
        MainThreadManager.Run(() =>
        {
            var level = args.GetNext<float>();
            this._setInkLevel(level);
        });

    }

    private void _sendPath(string json) {
        var model = JsonUtility.FromJson<PathModel>(json);

        var line = Instantiate(this.lineTemplate, new Vector3(0, 0, 0), new Quaternion());
        line.tag = "dynamic";

        var buildPath = line.GetComponent<BuildPath>();
        buildPath.Path = model.path;
    }

    private void _resetLevel() {
        Debug.Log("Deleting all the things!");
        var toDelete = GameObject.FindGameObjectsWithTag("dynamic");
        Debug.Log($"Deleting {toDelete.Length} Objects");
        foreach (var obj in toDelete)
        {
            Destroy(obj);
        }

        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.ResetPlayerPosition();
        }
    }

    private void _setInkLevel(float level)
    {
        FindObjectOfType<Painter>().setInkLevelPercent(level);
    }

    private void _setAttempts(int num)
    {
        this.Attempts = num;
        FindObjectOfType<Painter>().setAttempts(num);
    }

    private void _setLevel(int levelId)
    {
        if (this.curLevelObj != null)
        {

            Destroy(this.curLevelObj);
        }

        GameObject.Find("LevelComplete").GetComponent<SpriteRenderer>().enabled = false;
        FindObjectOfType<Timer>().ResetTime();

        this.curLevel = levels.Find(x => x.id == levelId);
        this.levelText.text = this.curLevel.id.ToString();
        Debug.Log($"Current level: id: {curLevel.id}, {curLevel.MaxInk}");

        this.curLevelObj = Instantiate(curLevel.LevelPrefab, GameObject.Find("LevelZaddy").transform);
        this.curLevelObj.transform.Find("Character").GetComponent<PlayerController>().network = this;

        Painter p = FindObjectOfType<Painter>();
        p.maxInk = this.curLevel.MaxInk;
        p.readOnly = false;

        GameObject player = this.curLevelObj.transform.Find("Character").gameObject;
        player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        var walls = GameObject.FindGameObjectsWithTag("wall");
        if (!Debugging)
        {
            if (!NetworkManager.Instance.IsServer)
            {
                this.curLevelObj.transform.Find("Character").gameObject.SetActive(false);
                foreach (var wall in walls)
                {
                    wall.GetComponent<SpriteRenderer>().enabled = false;
                }
            } else
            {
                p.readOnly = true;
            }
        }
    }

    private void _victory() {
        GameObject.Find("LevelComplete").GetComponent<SpriteRenderer>().enabled = true;
        FindObjectOfType<Painter>().readOnly = true;
        GameObject oldPlayer = this.curLevelObj.transform.Find("Character").gameObject;
        oldPlayer.SetActive(true);
        oldPlayer.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var walls = GameObject.FindGameObjectsWithTag("wall");

        foreach (var wall in walls)
        {
            wall.GetComponent<SpriteRenderer>().enabled = true;
        }
    }
}
