using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>シンプルなモーダルダイアログ</summary>
public class ModalDialog : MonoBehaviour {

	#region Static

	private static GameObject prefab;

	public static ModalDialog Create (Transform parent, string message, UnityAction action = null) {
		if (prefab == null) {
			prefab = Resources.Load<GameObject> ("ModalDialog");
		}
		var instance = Instantiate (prefab, parent).GetComponent<ModalDialog> ();
		instance.init (message, action);
		return instance;
	}

	#endregion

	[SerializeField] private Text Message = default;

	private UnityAction okAction;

	private void init (string message, UnityAction action) {
		Message.text = message;
		okAction = action;
	}

	public void OnPushOKButton () {
		okAction?.Invoke ();
		Destroy (gameObject);
	}

	private void OnDestroy () {
		okAction = null;
	}

}