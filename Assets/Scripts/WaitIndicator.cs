using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// �������̕\���Ƒ���̃u���b�N
///		��{�I�Ȏg����
///			WaitIndicator.display = true; // �\��
///			WaitIndicator.display = false; // ����
///		�I�v�V���� (���炩���ߎ��s���邱�ƂŐݒ肷��)
///			WaitIndicator.Create (transform.parent); // �e
///			WaitIndicator.Create (message: "busy..."); // ����
///			WaitIndicator.Create (transform.parent, "busy..."); // �e�ƕ���
/// </summary>
public class WaitIndicator : MonoBehaviour {

	#region Static

	/// <summary>�v���t�@�u</summary>
	private static GameObject prefab;
	/// <summary>�V���O���g���C���X�^���X</summary>
	private static WaitIndicator instance;
	/// <summary>�ŏ��Ɍ��������L�����o�X</summary>
	private static Transform canvas;

	/// <summary>
	/// �\���̐؂�ւ�
	///		���������Ȃ�C���X�^���X����������
	/// </summary>
	public static bool display {
		get => instance?.gameObject.activeSelf == true;
		set {
			instance ??= Create ();
			instance.gameObject.SetActive (value);
        }
    }

	/// <summary>�����I��(��)������</summary>
	/// <param name="parent">�R���e�i �^�����Ȃ���ΓK���ɒT�����L�����o�X���g�p</param>
	/// <param name="message">��փe�L�X�g �^�����Ȃ���΃v���t�@�u�̂܂�</param>
	/// <returns>�������ꂽ�C���X�^���X</returns>
	public static WaitIndicator Create (Transform parent = null, string message = null) {
		prefab ??= Resources.Load<GameObject> ("WaitIndicator");
		canvas ??= FindObjectOfType<Canvas> ()?.transform;
		if (instance) {
			Destroy (instance.gameObject);
			instance = null;
		}
		instance = Instantiate (prefab, parent ?? canvas).GetComponent<WaitIndicator> ();
		instance.init (message);
		return instance;
	}

	#endregion

	/// <summary>���b�Z�[�W���i�[�����UI�e�L�X�g</summary>
	[SerializeField] private Text Message = default;

	/// <summary>�C���X�^���X�̏�����</summary>
	/// <param name="message">��փe�L�X�g</param>
	private void init (string message) {
		if (!string.IsNullOrEmpty (message) && Message != null) {
			Message.text = message;
		}
	}

}