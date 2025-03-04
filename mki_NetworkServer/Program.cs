using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace mki_NetworkServer
{
    class Program
    {
        private static Dictionary<TcpClient, NetworkClient> clients = new Dictionary<TcpClient, NetworkClient>();
        private static Dictionary<int, Room> rooms = new Dictionary<int, Room>();
        private static Dictionary<int, RoomInfo> roomInfos = new Dictionary<int, RoomInfo>();
        private static string address = @$"{Directory.GetCurrentDirectory()}/log.txt";

        static void Main(string[] args)
        {
            Thread thread = new Thread(ListenerThread);
            thread.Start();
        }

        private static void ListenerThread()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 5500);
            int count = 0;

            try
            {
                listener.Start();
                while (true)
                {
                    int id = ++count;
                    TcpClient tc = listener.AcceptTcpClient();

                    Thread th = new Thread(() => Send(tc, id));
                    th.Start();

                    clients.Add(tc, new NetworkClient()
                    {
                        id = id,
                        client = tc,
                        stream = tc.GetStream(),
                        thread = th
                    });

                    Console.WriteLine($"Conneted | ID: {id} / IP : {((IPEndPoint)tc.Client.RemoteEndPoint).Address}");
                    Log($"Conneted | ID: {id} / IP : {((IPEndPoint)tc.Client.RemoteEndPoint).Address}");
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void Send(TcpClient tc, int id)
        {
            byte[] buffer = new byte[1024];
            NetworkStream stream = tc.GetStream();

            int bytes;
            while ((bytes = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (!Encoding.UTF8.GetString(buffer).Contains("\\ENDBUFFER\\")) continue;

                try
                {
                    Box box = JsonConvert.DeserializeObject<Box>(Encoding.UTF8.GetString(buffer).Replace("\\ENDBUFFER\\", ""));
                    Log(Encoding.UTF8.GetString(buffer).Replace("\\ENDBUFFER\\", ""));

                    if (box.command.Contains("room."))
                        Room(box, tc);
                    else
                    {
                        if(clients[tc].curRoomID != -1)
                        {
                            if(rooms[clients[tc].curRoomID] != null)
                                rooms[clients[tc].curRoomID].BroadCast(buffer, bytes);
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(Encoding.UTF8.GetString(buffer));
                }
                Console.WriteLine($"[{DateTime.Now}] IP {((IPEndPoint)tc.Client.RemoteEndPoint).Address} / read {bytes}");
                buffer = new byte[1024];
            }
            Console.WriteLine($"Disconneted | ID: {id} / IP : {((IPEndPoint)tc.Client.RemoteEndPoint).Address}");
            Log($"Disconneted | ID: {id} / IP : {((IPEndPoint)tc.Client.RemoteEndPoint).Address}");
            if (clients[tc].curRoomID != -1)
            {
                RemoveRoomInfo(clients[tc].curRoomID);
            }

            stream.Close();
            tc.Close();
        }

        private static void Log(string content)
        {
            StreamWriter writer = new StreamWriter(address, true);

            writer.WriteLine($"[{DateTime.Now}] {content}");
            writer.Close();
        }

        private static void Room(Box box, TcpClient tc)
        {
            string command = box.command.Replace("room.", "");
            NetworkClient nc = clients[tc];

            switch(command)
            {
                case "make":
                    MakeRoom(nc, box);
                    break;
                case "connect":
                    ConnectRoom(tc, (int)(long)box.data);
                    break;
                case "exit":
                    if (rooms[nc.curRoomID].Exit(nc)) RemoveRoomInfo(nc.curRoomID);
                    break;
                case "swap":
                    rooms[nc.curRoomID].Swap();
                    break;
                case "delete":
                    if(rooms[nc.curRoomID] != null)
                    {
                        rooms[nc.curRoomID].Delete();
                        roomInfos.Remove(nc.curRoomID);
                    }
                    break;
                case "refresh":
                    RefreshRoom(tc);
                    break;
                case "curInfo":
                    Send(tc, "info.room", new Basic(rooms[nc.curRoomID].GetPersonnel(), rooms[nc.curRoomID].roomName));
                    break;
            }
        }

        private static void MakeRoom(NetworkClient nc, Box box)
        {
            Room room = new Room(nc, box.data.ToString());
            rooms[room.roomID] = room;
            roomInfos[room.roomID] = (new RoomInfo(room.roomID, room.roomName, room.GetPersonnel()));

            Console.WriteLine($"Create Room : {box.data}");

            nc.curRoomID = room.roomID;
        }

        private static void ConnectRoom(TcpClient tc, int id)
        {
            if (rooms[id] == null) Send(tc, "try.room", "False"); ;

            bool connect = rooms[id].TryConnect(clients[tc]);
            if (connect)
            {
                clients[tc].curRoomID = rooms[id].roomID;
            }
            Send(tc, "try.room", connect.ToString());
        }

        private static void RefreshRoom(TcpClient tc)
        {
            foreach(RoomInfo r in roomInfos.Values)
                if(r != null)
                    r.personnel = rooms[r.roomID].GetPersonnel();

            string roomsInfo = JsonConvert.SerializeObject(roomInfos.Values);
            Send(tc, "refresh.room", roomsInfo);
        }

        private static void Send(TcpClient tc, string type, object data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"{new Box(type, data).ToJson()}\\ENDBUFFER\\");
            Log(new Box(type, data).ToJson());
            tc.GetStream().Write(buffer, 0, buffer.Length);
        }

        public static void RemoveRoomInfo(int id)
        {
            foreach (RoomInfo r in roomInfos.Values)
            {
                if (r == null) continue;

                if (r.roomID == id)
                {
                    roomInfos[id] = null;
                    rooms[id]?.Delete();
                    rooms[id] = null;
                    break;
                }
            }
        }
    }
}
