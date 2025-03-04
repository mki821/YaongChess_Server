using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Newtonsoft.Json;

namespace mki_NetworkServer
{
    class NetworkClient
    {
        public int id;
        public TcpClient client;
        public NetworkStream stream;
        public Thread thread;
        public int curRoomID = -1;
    }

    class Box
    {
        public string command;
        public object data;

        public Box(string command, object data)
        {
            this.command = command;
            this.data = data;
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
    }

    [Serializable]
    class Basic
    {
        public object obj1;
        public object obj2;

        public Basic(object obj1, object obj2)
        {
            this.obj1 = obj1;
            this.obj2 = obj2;
        }
    }

    [Serializable]
    class RoomInfo
    {
        public int roomID;
        public string roomName;
        public int personnel;

        public RoomInfo(int id, string name, int personnel)
        {
            roomID = id;
            roomName = name;
            this.personnel = personnel;
        }
    }

    class Room
    {
        private static int id = 0;

        public int roomID;
        public string roomName;
        public NetworkClient host;
        public NetworkClient[] clients = new NetworkClient[2];

        public Room(NetworkClient host, string roomName)
        {
            roomID = id++;
            this.host = host;
            this.roomName = roomName;
            clients[0] = host;

            byte[] buffer = Encoding.UTF8.GetBytes($"{new Box("success.room", true).ToJson()}\\ENDBUFFER\\");
            host.client.GetStream().Write(buffer, 0, buffer.Length);
            SetTeam(host.client, 0);
        }

        public bool TryConnect(NetworkClient client)
        {
            Check();
            if (clients[0] == null && clients[1] == null) return false;

            if (clients[0] == null)
            {
                clients[0] = client;
                SetTeam(client.client, 0);
                return true;
            }
            else if (clients[1] == null)
            {
                clients[1] = client;
                SetTeam(client.client, 1);
                return true;
            }
            return false;
        }

        public int GetPersonnel()
        {
            Check();
            int count = 0;
            if (clients[0] != null)
                count++;
            if (clients[1] != null)
                count++;

            return count;
        }

        public void BroadCast(byte[] buffer, int size)
        {
            Check();
            clients[0]?.client.GetStream().Write(buffer, 0, size);
            clients[1]?.client.GetStream().Write(buffer, 0, size);
        }

        public void Swap()
        {
            Check();
            (clients[0], clients[1]) = (clients[1], clients[0]);

            if(clients[0] != null)
                SetTeam(clients[0].client, 0);
            if(clients[1] != null)
                SetTeam(clients[1].client, 1);
        }

        public void Delete()
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"{new Box("disconnect.room", 0).ToJson()}\\ENDBUFFER\\");

            BroadCast(buffer, buffer.Length);
        }

        private void Check()
        {
            if (clients[0] != null)
                if (!clients[0].client.Connected)
                {
                    clients[0] = null;
                }
            if (clients[1] != null)
                if (!clients[1].client.Connected)
                {
                    clients[1] = null;
                }
        }

        public void SetTeam(TcpClient tc, int team)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"{new Box("set.team", team).ToJson()}\\ENDBUFFER\\");
            tc.GetStream().Write(buffer, 0, buffer.Length);

            buffer = Encoding.UTF8.GetBytes($"{new Box("wyn.lobby", null).ToJson()}\\ENDBUFFER\\");
            tc.GetStream().Write(buffer, 0, buffer.Length);
        }

        public bool Exit(NetworkClient nc) {
            Check();
            if (clients[0] == nc)
            {
                clients[0] = null;
            }
            else if (clients[1] == nc)
            {
                clients[1] = null;
            }

            if(clients[0] == null && clients[1] == null)
            {
                Delete();
                return true;
            }

            return false;
        }
    }
}
