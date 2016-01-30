﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;

public class logicScript : MonoBehaviour
{
	public GameObject introCanvas;
	//public GameObject intro2Canvas;
	public GameObject playCanvas;
	public GameObject resultCanvas;
	public GameObject endCanvas;
	public GameObject uiPlayLeft;
	public GameObject uiPlayRight;
	public GameObject prefabPlayer;

	public GameObject sounds;
	private audioScript soundScript;
	public Text txtMessageIntro;
	public Text txtMessagePlay;
	public Text txtMessageEnd;
	public Text txtMessageResult;

	private float timeStateChange;

	private playersScript players;

	public delegate void StateChangeAction();
	public static event StateChangeAction OnStateChange;

	int[] karma;
	List<clScene> lstScene = new List<clScene>();
	clScene curScene;

	void Awake()
	{
		AirConsole.instance.onConnect += Instance_onConnect;
		AirConsole.instance.onCustomDeviceStateChange += Instance_onCustomDeviceStateChange;
		AirConsole.instance.onDeviceStateChange += Instance_onDeviceStateChange;
		AirConsole.instance.onDisconnect += Instance_onDisconnect;
		AirConsole.instance.onMessage += Instance_onMessage;
		AirConsole.instance.onReady += Instance_onReady;
		init();
	}

	private void Instance_onReady(string code)
	{
		Debug.Log("Ready " + code);
	}

	private void init()
	{
		Gvar.init();
		soundScript = sounds.GetComponent<audioScript>();
		players = new playersScript();
		setGameState(enGameState.Intro);
		karma = new int[8];
	}

	private void setGameState(enGameState state)
	{
		if (Gvar.gameState != state)
		{
			soundScript.play(0);
			Gvar.setGameState(state);
			timeStateChange = Time.time;
			if (OnStateChange != null)
				OnStateChange();

			hideAllCanvas();
			switch (state)
			{
				case enGameState.Intro:
					introCanvas.SetActive(true);
					if (AirConsole.instance.GetActivePlayerDeviceIds.Count > 0)
					{
						AirConsole.instance.Broadcast(Cmd.Intro + Lng.Intro);
					}
					lstScene.Clear();
					break;
				case enGameState.Intro2:
					introCanvas.SetActive(true);
					players.sendRoles();
					break;
				case enGameState.Play:
					playCanvas.SetActive(true);

					setNewScene();
					break;
				case enGameState.Result:
					List<clAnswer> lstA = players.getResult();
					AirConsole.instance.Broadcast(Cmd.Debug + "Help:" + lstA[0].score + " Block:" + lstA[1].score + " Do nothing:" + lstA[2].score);
					List<int> lstR = new List<int>();
					int max = 0;
					foreach (clAnswer item in lstA)
					{
						if (item.score > max)
							max = item.score;
					}
					for (int i = 0; i < lstA.Count; i++)
					{
						if (lstA[i].score != max)
						{
							lstA.RemoveAt(i);
							i--;
						}
					}

					clAnswer result = null;
					if(lstA.Count == 0)
					{
						Debug.Log("No result...");
						return;
					}
					else
					{
						result = lstA[Random.Range(0, lstA.Count)];
					}
					AirConsole.instance.Broadcast(Cmd.Debug + "Final choice:" + Gvar.actionStr[result.id]);
					addScore(result);
					resultCanvas.SetActive(true);

					txtMessageResult.text = curScene.action.getTextResult(result.id);
					AirConsole.instance.Broadcast(Cmd.Result);
					break;
				case enGameState.End:
					List<clAnswer> lstF = new List<clAnswer>();
					for (int i = 0; i < karma.Length; i++)
					{
						lstF.Add(new clAnswer(i, karma[i]));
					}

					lstF.Sort();

					string endText = "";
					foreach (clAnswer item in lstF)
					{
						endText += item.score + " - " + Gvar.emotionStr[item.id] + "\r\n";
					}

					txtMessageEnd.text = endText;
					endCanvas.SetActive(true);
					if (AirConsole.instance.GetActivePlayerDeviceIds.Count > 0)
					{
						AirConsole.instance.Broadcast(Cmd.End + "Le futur text de fin");
					}

					break;
				default:
					break;
			}
		}
	}

	private void addScore(clAnswer result)
	{
		clResult r = curScene.getResult(result);

		if(r == null)
		{
			Debug.Log("No result found");
			return;
		}

		string d = "";
		for (int i = 0; i < karma.Length; i++)
		{
			karma[i] += r.val[i];
			d += Gvar.emotionStr[i] + ":" + karma[i] + " ";
		}

		AirConsole.instance.Broadcast(Cmd.Debug + d);
	}

	private void setNewScene()
	{
		players.resetScene();
		curScene = new clScene();
		lstScene.Add(curScene);

		txtMessagePlay.text = curScene.place.descEN;
		AirConsole.instance.Broadcast(Cmd.Play + curScene.introText);
		AirConsole.instance.Broadcast(Cmd.But + "0|" + curScene.action.help);
		AirConsole.instance.Broadcast(Cmd.But + "1|" + curScene.action.block);
		AirConsole.instance.Broadcast(Cmd.But + "2|" + curScene.action.doNothing);
		//AirConsole.instance.Broadcast(Cmd.But + "3|"+ Lng.Answer4);
	}

	private void hideAllCanvas()
	{
		introCanvas.SetActive(false);
		//intro2Canvas.SetActive(false);
		playCanvas.SetActive(false);
		resultCanvas.SetActive(false);
		endCanvas.SetActive(false);
	}

	private void Instance_onMessage(int from, JToken data)
	{
		int numPlayer = AirConsole.instance.ConvertDeviceIdToPlayerNumber(from);

		if (!players.exists(numPlayer))
		{
			Debug.Log("Player " + numPlayer + " doesn't exists");
			return;
		}
		string curCmd = data.Value<string>("cmd");
		Debug.Log("CMD: " + curCmd);

		if(curCmd == "restart")
		{
			setGameState(enGameState.Intro);
		}

		switch (Gvar.gameState)
		{
			case enGameState.None:
				break;
			case enGameState.Intro:
				if (curCmd == "start")
				{
					players.reset();
					setGameState(enGameState.Intro2);
				}
				break;
			case enGameState.Intro2:
				if (curCmd == "ready")
				{
					Debug.Log("Set ready");
					players.setReady(numPlayer);
				}
				if (players.allReady())
					setGameState(enGameState.Play);
				break;
			case enGameState.Play:
				msgResponse r = players.treatMessage(numPlayer, curCmd);
				if(players.allAnswered())
				{
					setGameState(enGameState.Result);
				}
				break;
			case enGameState.Result:
				if (curCmd == "next")
				{
					if (lstScene.Count >= 5)
						setGameState(enGameState.End);
					else
						setGameState(enGameState.Play);
				}
				break;
			case enGameState.End:
				if (curCmd == "end")
					setGameState(enGameState.Intro);
				break;
			default:
				break;
		}
	}







	private void Instance_onDisconnect(int device_id)
	{
		Debug.Log("Disconnected " + device_id);

		players.delete(this, device_id);
		arrangePlayer();
	}

	private void Instance_onDeviceStateChange(int device_id, JToken user_data)
	{
		//	Debug.Log("State change " + device_id + " " + user_data.ToString());
	}

	private void Instance_onCustomDeviceStateChange(int device_id, JToken custom_device_data)
	{
		//Debug.Log("Custom device state change " + device_id + " " + custom_device_data.ToString());
	}

	private void Instance_onConnect(int device_id)
	{
		AirConsole.instance.SetActivePlayers(Gvar.nbPlayerMax);

		Debug.Log("Connected " + device_id);

		playerScript cur = players.add(this, device_id);
		arrangePlayer();

		if (Gvar.gameState == enGameState.Intro)
		{
			AirConsole.instance.Message(device_id, Cmd.Intro + Lng.Intro);
		}
		else if (Gvar.gameState == enGameState.Intro2)
		{
			cur.setRole();
		}
		else
		{
			AirConsole.instance.Message(device_id, Cmd.Intro + Lng.Wait);
		}
	}

	private void arrangePlayer()
	{
		List<playerScript> lstP = players.getList();

		for (int i = 0; i < lstP.Count; i++)
		{
			if (i < 4)
			{
				lstP[i].transform.SetParent(uiPlayLeft.transform);
				RectTransform r = lstP[i].GetComponent<RectTransform>();
				r.offsetMin = new Vector2(0, -110 + i * -115);
				r.offsetMax = new Vector2(0, 0 + i * -115);
				r.localScale = new Vector3(1f, 1f, 1f);
			}
			else
			{
				lstP[i].transform.SetParent(uiPlayRight.transform);
				RectTransform r = lstP[i].GetComponent<RectTransform>();
				r.offsetMin = new Vector2(0, -110 + i * -115);
				r.offsetMax = new Vector2(0, 0 + i * -115);
				r.localScale = new Vector3(1f, 1f, 1f);
			}
		}

	}


	// Use this for initialization
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		//if(Time.time - timeStateChange > 5.0)
		//{
		//	soundScript.play(0);
		//	timeStateChange = Time.time;
		//}

		switch (Gvar.gameState)
		{
			case enGameState.Intro:
				break;
			case enGameState.Play:
				break;
			case enGameState.End:
				break;
			default:
				break;
		}
	}

}
