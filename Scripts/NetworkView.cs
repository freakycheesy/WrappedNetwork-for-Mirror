using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Mirror;

namespace UnityEngine.Networking {
#if UNITY_EDITOR
    [CustomEditor(typeof(NetworkView)), CanEditMultipleObjects]
    public class NetworkViewEditor : Editor {
        public override void OnInspectorGUI() {
            var target = (NetworkView)this.target;
            EditorGUILayout.BeginVertical((GUIStyle)"HelpBox");
            GUILayout.Label("Network View", EditorStyles.boldLabel);
            if (Application.IsPlaying(target) && targets.Length < 2) {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Id", target.Id);
                var owner = JsonUtility.ToJson(target.Owner, true);
                EditorGUILayout.TextField("Owner", owner);
                EditorGUI.EndDisabledGroup();
            }
            else {
                EditorGUILayout.HelpBox("Enter Play Mode for more Info", MessageType.Warning);
            }
            if (GUILayout.Button("Scan MonoBehaviours")) {
                foreach (var _target in targets) {
                    EditorUtility.SetDirty(_target);
                    var view = _target as NetworkView;
                    view.ScanForBehaviours();
                    AssetDatabase.SaveAssetIfDirty(_target);
                }
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndVertical();
            base.OnInspectorGUI();
        }
    }
#endif
    [DefaultExecutionOrder(-64)]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkView : NetworkBehaviour
    {
        public static Dictionary<uint, NetworkView> Views { get; set; } = new();
        public bool IsMine => isOwned || isLocalPlayer;
        public NetworkConnectionToClient Owner => connectionToClient;
        public int Id => (int)netId;
        public bool AllowClientAuthorityOverride { get; set; } = true;
        public Action OnStart {
            get; set;
        }
        private Dictionary<string, Method> wrappedMethods { get; set; } = new();
        [SerializeField]
        private MonoBehaviour[] behavioursToBeScanned;
        private void OnEnable() {
            Views.Add(netId, this);
        }
        private void OnDisable() {
            Views.Remove(netId);
        }
        private struct Method
        {
            public MonoBehaviour source; public MethodInfo methodInfo; public bool wantSenderInfo;
            public Method(MonoBehaviour source, MethodInfo methodInfo, bool wantSenderInfo)
            {
                this.source = source;
                this.methodInfo = methodInfo;
                this.wantSenderInfo = wantSenderInfo;
            }
        }     
        private void Awake()
        {
            wrappedMethods = new();
            foreach (MonoBehaviour component in behavioursToBeScanned)
                foreach (var method in component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => Attribute.IsDefined(method, typeof(RPC)))
                .ToList())
                    wrappedMethods.Add(method.Name,
                        new Method(component
                        , method
                        , method.GetParameters().Length == 0 ? false : method.GetParameters().Last().ParameterType.Equals(typeof(NetworkConnectionToClient))));
        }
        public void ScanForBehaviours()
        {
            behavioursToBeScanned = GetComponentsInChildren<MonoBehaviour>(true)
                .Where(comp => comp.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => Attribute.IsDefined(method, typeof(RPC)))).ToArray();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!GetComponent<NetworkIdentity>().enabled)
                GetComponent<NetworkIdentity>().enabled = true;
            OnStart?.Invoke();
        }

        [Command(requiresAuthority = false)]
        public void RequestOwnership(NetworkConnectionToClient conn = null) {
            netIdentity.RemoveClientAuthority();
            netIdentity.AssignClientAuthority(conn);
        }

        public void RPC(string methodName, RpcTarget target, params object[] args)
        {
            if (wrappedMethods[methodName].wantSenderInfo)
                args = args.Append(Network.LocalPlayer).ToArray();
            switch (target)
            {
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
        public void RPC(string methodName, int target, params object[] args)
        {
            TargetRPC(NetworkServer.connections[target], methodName, args);
        }


        [Command(requiresAuthority = false)]
        private void ServerRPC(string methodName, object[] args)
        {
            FinalInvoke(methodName, args);
        }
        [Command(requiresAuthority = false)]
        private void OthersRPC(string methodName, object[] args, int sender)
        {
            FinalOthersRPC(methodName, args, sender);
        }
        [ClientRpc]
        private void FinalOthersRPC(string methodName, object[] args, int sender)
        {
            if (Network.LocalPlayer.connectionId == sender)
                return;
            FinalInvoke(methodName, args);
        }

        [Command(requiresAuthority = false)]
        private void AllRPC(string methodName, object[] args)
        {
            AllClientsRPC(methodName, args);
        }

        [ClientRpc]
        private void AllClientsRPC(string methodName, object[] args)
        {
            FinalInvoke(methodName, args);
        }

        [TargetRpc]
        private void TargetRPC(NetworkConnectionToClient target, string methodName, object[] args)
        {
            FinalInvoke(methodName, args);
        }

        private void FinalInvoke(string methodName, object[] args)
        {
            wrappedMethods[methodName].methodInfo.Invoke(wrappedMethods[methodName].source, args);
        }

    }
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RPC : Attribute
    { }
    public enum RpcTarget { MasterClient, Others, All }
}