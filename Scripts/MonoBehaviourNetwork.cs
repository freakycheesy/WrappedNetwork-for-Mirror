using Mirror;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;


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
        var networkIdentity = target.GetComponentInParent<NetworkIdentity>();
        if (networkIdentity) {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("NetworkIdentity", networkIdentity, typeof(NetworkIdentity), true);
            EditorGUI.EndDisabledGroup();
        }
        else {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add NetworkIdentity")) {
                AddNetworkIdentity(target.transform);
            }
            if (GUILayout.Button("Add NetworkIdentity Root")) {
                AddNetworkIdentity(target.transform.root);
            }
            GUILayout.EndHorizontal();
            void AddNetworkIdentity(Transform target) {
                var root = target.gameObject;
                EditorUtility.SetDirty(root);
                networkIdentity = root.AddComponent<NetworkIdentity>();
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
        public bool IsMine => isOwned || isLocalPlayer;
        public NetworkConnectionToClient Owner => connectionToClient;
        public NetworkConnection LocalPlayer => Network.LocalPlayer;
        public int Id => (int)netId;
        public virtual bool AllowClientAuthorityOverride {
            get;
        } = false;
        private Dictionary<string, Method> wrappedMethods { get; set; } = new();
        private struct Method {
            public MonoBehaviour source; public MethodInfo methodInfo;
            public Method(MonoBehaviour source, MethodInfo methodInfo, bool wantSenderInfo) {
                this.source = source;
                this.methodInfo = methodInfo;
            }
        }
        private void Awake() {
            wrappedMethods = new();
            foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => Attribute.IsDefined(method, typeof(RPC)))
                .ToList())
                wrappedMethods.Add(method.Name,
                    new Method(this
                    , method
                    , method.GetParameters().Length == 0 ? false : method.GetParameters().Last().ParameterType.Equals(typeof(NetworkConnectionToClient))));
        }
        public override void OnStartClient() {
            base.OnStartClient();
            if (!GetComponent<NetworkIdentity>().enabled)
                GetComponent<NetworkIdentity>().enabled = true;
        }

        [Command(requiresAuthority = false)]
        public void RequestOwnership(NetworkConnectionToClient conn = null) {
            netIdentity.RemoveClientAuthority();
            netIdentity.AssignClientAuthority(conn);
        }

        public void RPC(string methodName, RpcTarget target, params object[] args) {
            switch (target) {
                case RpcTarget.MasterClient:
                    ServerRPC(methodName, args);
                    break;
                case RpcTarget.Others:
                    OthersRPC(methodName, args, Network.LocalPlayer.connectionId);
                    break;
                case RpcTarget.All:
                    AllRPC(methodName, args);
                    break;
            }
        }
        public void RPC(string methodName, int target, params object[] args) {
            TargetRPC(NetworkServer.connections[target], methodName, args);
        }


        [Command(requiresAuthority = false)]
        private void ServerRPC(string methodName, object[] args) {
            FinalInvoke(methodName, args);
        }
        [Command(requiresAuthority = false)]
        private void OthersRPC(string methodName, object[] args, int sender) {
            FinalOthersRPC(methodName, args, sender);
        }
        [ClientRpc]
        private void FinalOthersRPC(string methodName, object[] args, int sender) {
            if (Network.LocalPlayer.connectionId == sender)
                return;
            FinalInvoke(methodName, args);
        }

        [Command(requiresAuthority = false)]
        private void AllRPC(string methodName, object[] args) {
            AllClientsRPC(methodName, args);
        }

        [ClientRpc]
        private void AllClientsRPC(string methodName, object[] args) {
            FinalInvoke(methodName, args);
        }

        [TargetRpc]
        private void TargetRPC(NetworkConnectionToClient target, string methodName, object[] args) {
            FinalInvoke(methodName, args);
        }

        private void FinalInvoke(string methodName, object[] args) {
            wrappedMethods[methodName].methodInfo.Invoke(wrappedMethods[methodName].source, args);
        }

    }
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RPC : Attribute {
    }
    public enum RpcTarget {
        MasterClient, Others, All
    }
}
