using Mirror;
using System.Linq;
using UnityEngine.SceneManagement;

namespace UnityEngine.Networking
{
    public static partial class Network
    {
        public static string NickName {
            get;set;
        }
        public static bool OfflineMode {
            get => NetworkServer.listen;
            set => NetworkServer.listen = !value;
        }
        public static bool IsConnected => NetworkServer.active || NetworkClient.active;
        public static bool IsHost => NetworkServer.active;
        public static NetworkConnectionToClient LocalPlayer => NetworkServer.localConnection;
        public static int LocalID => LocalPlayer.connectionId;

        public static NetworkConnectionToClient[] PlayerList => NetworkServer.connections.Values.ToArray();

        [Server]
        public static GameObject Instantiate(string name, Vector3 position, Quaternion rotation, NetworkConnectionToClient owner = null) {
            return Instantiate(Resources.Load<GameObject>(name),position, rotation, owner);
        }
        [Server]
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnectionToClient owner = null) {
            var gameObject = Object.Instantiate(prefab, position, rotation);
            NetworkServer.Spawn(gameObject, owner);
            return gameObject;
        }
        [Server]
        public static void Destroy(NetworkView view) {
            NetworkServer.Destroy(view.gameObject);
        }
        [Server]
        public static void Destroy(GameObject gameObject) {
            NetworkServer.Destroy(gameObject);
        }
        [Server]
        public static void DestroyAll() {
            foreach (var view in NetworkView.Views.Values) {
                Destroy(view);
            }
        }
        public static void Disconnect() {
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();
        }
        public static void LoadLevel(string sceneName) {
            if (!NetworkServer.active)
                return;
            var scene = SceneManager.GetSceneByName(sceneName);
            LoadScene(scene);
        }
        public static void LoadLevel(int index) {
            if (!NetworkServer.active)
                return;
            var scene = SceneManager.GetSceneByBuildIndex(index);
            LoadScene(scene);
        }

        private static void LoadScene(Scene scene) {
            NetworkManager.singleton.ServerChangeScene(scene.path);
        }

        public static void CreateRoom(int maxConns = 16, bool asHost = true) {
            Disconnect();
            NetworkManager.singleton.maxConnections = maxConns;
            if(asHost) NetworkManager.singleton.StartHost();
            else NetworkManager.singleton.StartServer();
        }
        public static void JoinRoom(string address) {
            Disconnect();
            NetworkManager.singleton.networkAddress = address;
            NetworkManager.singleton.StartClient();
        }
    }
}
