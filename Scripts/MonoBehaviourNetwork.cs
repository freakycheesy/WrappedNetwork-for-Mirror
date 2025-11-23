using Mirror;
using UnityEngine;
using System;


#if UNITY_EDITOR
using UnityEngine.Networking;
using UnityEditor;

[CustomEditor(typeof(MonoBehaviourNetwork), true)]
public class MonoBehaviourNetworkEditor : Editor {

    public override void OnInspectorGUI() {
        EditorGUILayout.BeginVertical((GUIStyle)"HelpBox");
        var target = (MonoBehaviourNetwork)this.target;
        var script = MonoScript.FromMonoBehaviour(target);
        GUILayout.Label(script.name, EditorStyles.boldLabel);
        var networkView = target.GetComponentInParent<NetworkView>();
        if (networkView) {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(networkView, typeof(NetworkView), true);
            EditorGUI.EndDisabledGroup();
        }
        else {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add NetworkView")) {
                AddNetworkView(target.transform);
            }
            if (GUILayout.Button("Add NetworkView Root")) {
                AddNetworkView(target.transform.root);
            }
            GUILayout.EndHorizontal();
            void AddNetworkView(Transform target) {
                var root = target.gameObject;
                EditorUtility.SetDirty(root);
                networkView = root.AddComponent<NetworkView>();
                AssetDatabase.SaveAssetIfDirty(target.transform.root);
            }
        }
        EditorGUILayout.EndVertical();
        base.OnInspectorGUI();
    }
}
#endif
namespace UnityEngine.Networking {
    public abstract class MonoBehaviourNetwork : NetworkBehaviour
    {
#if UNITY_EDITOR
        [Obsolete("Use networkView instead of view")]
        public NetworkView view => networkView;
#endif
        public new NetworkView networkView {
            get; internal set;
        }
        public bool IsMine => networkView.IsMine;
        public bool IsHost => Network.IsHost;
        
        void Start() {
            networkView = GetComponentInParent<NetworkView>();
        }
    }
}
