using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace U3DUtility
{
    /// <summary>
    /// 接收数据包结构
    /// </summary>
    public struct Pkt
    {
        public short messId;    //消息id
        public byte[] data;     //序列化后的数据
    }

    /// <summary>
    /// 数据传输工具类
    /// </summary>
    public class TcpLayer : MonoBehaviour
    {
        class AsyncData
        {
            public int pos;
            public short messId;
            public byte[] buff;
        }

        public const int PACK_HEAD_SIZE = 4;
        public const int MSG_ID_SIZE = 2;

        public delegate void OnConnectEvent(bool isSuccess, string msg);
        public delegate void OnDisconnectEvent(string msg);
        public delegate void OnRecvEvent(int msgId, byte[] data);

        private TcpClient mTcpClient;
        private NetworkStream mNetStream = null;
        private bool mIsConnected = false;
        private OnConnectEvent mOnConnect;
        private OnDisconnectEvent mOnDisConnect;
        private OnRecvEvent mOnRecvPackage;

        //下面几个参数是用来重连时使用的参数
        private string mIP;     //当前连接的服务器IP
        private int mPort;      //当前连接的端口
        private int mSendBuffSize = 10240;  //发送缓冲大小
        private int mRecvBuffSize = 10240;  //接受缓冲大小

        private Queue<Pkt> mRecvPacks = new Queue<Pkt>();   //接收包暂存队列

        private static TcpLayer mSingleton = null;

        public static TcpLayer singleton
        {
            get
            {
                if (mSingleton == null)
                {
                    Loom.Initialize();

                    GameObject o = new GameObject("Tcp Connector");
                    DontDestroyOnLoad(o);
                    mSingleton = o.AddComponent<TcpLayer>();
                }

                return mSingleton;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="recvBuffSize">Socket接收缓冲区大小</param>
        /// <param name="sendBuffSize">Socket发送缓冲区大小</param>
        public void Init (int recvBuffSize, int sendBuffSize)
        {
            mSendBuffSize = sendBuffSize;
            mRecvBuffSize = recvBuffSize;
        }

        /// <summary>
        /// 新建连接
        /// </summary>
        /// <param name="ip">连接的服务器IP</param>
        /// <param name="port">连接的端口号</param>
        /// <param name="connEvent">连接完成后回调</param>
        /// <param name="disconnEvent">断开连接发生时回调</param>
        /// <param name="recvEvent">接收到消息包时回调</param>
        public void Connect(string ip, int port, OnConnectEvent connEvent, OnDisconnectEvent disconnEvent, OnRecvEvent recvEvent)
        {
            if (mIsConnected)
            {
                Disconnect("reconnect");
            }

            mOnConnect = connEvent;
            mOnDisConnect = disconnEvent;
            mOnRecvPackage = recvEvent;
            mIP = ip;
            mPort = port;

            mTcpClient = new TcpClient
            {
                NoDelay = true,
                ReceiveBufferSize = mRecvBuffSize,
                SendBufferSize = mSendBuffSize
            };

            mIsConnected = false;

            try
            {
                mTcpClient.BeginConnect(IPAddress.Parse(ip), port, new AsyncCallback(OnConnectCallback), mTcpClient);

                Invoke("ConnectTimeOutCheck", 3);
            }
            catch (Exception ex)
            {
                if (IsInvoking("ConnectTimeOutCheck"))
                {
                    CancelInvoke("ConnectTimeOutCheck");
                }

                mOnConnect?.Invoke(false, ex.ToString());
            }
        }

        /// <summary>
        /// 添加一个新的接收数据回调
        /// </summary>
        /// <param name="recvEvent">要添加的回调函数</param>
        public void AddRecvEvent (OnRecvEvent recvEvent)
        {
            mOnRecvPackage += recvEvent;
        }

        /// <summary>
        /// 移除一个接受数据回调
        /// </summary>
        /// <param name="recvEvent">要移除的回调函数</param>
        public void RemoveRecvEvent(OnRecvEvent recvEvent)
        {
            mOnRecvPackage -= recvEvent;
        }

        /// <summary>
        /// 发起重新连接
        /// </summary>
        public void Reconnect()
        {
            if (mIsConnected)
            {
                Disconnect("reconnect");
            }

            mTcpClient = new TcpClient
            {
                NoDelay = true,
                ReceiveBufferSize = mRecvBuffSize,
                SendBufferSize = mSendBuffSize
            };

            try
            {
                mTcpClient.BeginConnect(IPAddress.Parse(mIP), mPort, new AsyncCallback(OnConnectCallback), mTcpClient);

                Invoke("ConnectTimeOutCheck", 3);
            }
            catch (Exception ex)
            {
				if (IsInvoking("ConnectTimeOutCheck"))
                {
                    CancelInvoke("ConnectTimeOutCheck");
                }
				
                mOnConnect?.Invoke(false, ex.ToString());
            }
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="msg">断开原因字符串</param>
        public void Disconnect(string msg)
        {
            if (mIsConnected)
            {
                mNetStream.Close();
                mTcpClient.Close();
                mIsConnected = false;

                mOnDisConnect?.Invoke(msg);

                lock (mRecvPacks)
                {
                    mRecvPacks.Clear();
                }
            }
        }

        /// <summary>
        /// 发送数据包到服务器
        /// </summary>
        /// <param name="messId">数据包消息id</param>
        /// <param name="data">数据包内容</param>
        public void SendPack(short messId, byte[] data)
        {
            int length = data.Length + PACK_HEAD_SIZE + MSG_ID_SIZE;
            MemoryStream dataStream = new MemoryStream(length);
            BinaryWriter binaryWriter = new BinaryWriter(dataStream);

            binaryWriter.Write(data.Length + 2);
            binaryWriter.Write((short)messId);
            binaryWriter.Write(data, 0, (int)data.Length);

            dataStream.Seek((long)0, 0);
            binaryWriter.Close();
            dataStream.Close();

            byte[] sendBytes = dataStream.GetBuffer();

            try
            {
                mNetStream.Write(sendBytes, 0, length);
            }
            catch (Exception ex)
            {
                Disconnect(ex.ToString());
            }
        }

        /// <summary>
        /// 当连接完成后回调处理
        /// </summary>
        /// <param name="asyncResult">异步结果</param>
        void OnConnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                TcpClient tcpclient = asyncResult.AsyncState as TcpClient;

                if (tcpclient.Client != null)
                {
                    tcpclient.EndConnect(asyncResult);
                }
            }
            catch (Exception ex)
            {
                U3DUtility.Loom.QueueOnMainThread(() =>
                {
                    mOnConnect?.Invoke(false, ex.ToString());   //发生了异常，通知连接失败
                });
            }
            finally
            {
                mNetStream = mTcpClient.GetStream();

                BeginPackRead(); //开始异步读取包

                U3DUtility.Loom.QueueOnMainThread(() => //到主线程中通知连接成功
                {
                    if (IsInvoking("ConnectTimeOutCheck"))
                    {
                        CancelInvoke("ConnectTimeOutCheck");
                    }

                    mIsConnected = true;
                    mOnConnect?.Invoke(true, "");
                });
            }
        }

        void Update()
        {
            //通知所有注册的回调函数处理当前接收的包
            lock(mRecvPacks)
            {
                for (; mRecvPacks.Count > 0;)
                {
                    var pkt = mRecvPacks.Dequeue();
                    mOnRecvPackage?.Invoke(pkt.messId, pkt.data);
                }
            }
        }

        void ConnectTimeOutCheck()
        {
            if (!mIsConnected)
            {
                mOnConnect?.Invoke(false, "connect time out");
            }
        }

        /// <summary>
        /// 读取包头的异步回调
        /// </summary>
        /// <param name="asyncResult">异步参数</param>
        void ReadAsyncCallBackPackHead(IAsyncResult asyncResult)
        {
            try
            {
                int dataLen = mNetStream.EndRead(asyncResult);

                AsyncData head_data = (AsyncData)asyncResult.AsyncState;

                if (head_data.pos + dataLen == head_data.buff.Length) //如果包头读取完毕则开始读取数据部分
                {
                    int packLen = new BinaryReader(new MemoryStream(head_data.buff)).ReadInt32();
                    short msgID = new BinaryReader(new MemoryStream(head_data.buff, PACK_HEAD_SIZE, MSG_ID_SIZE)).ReadInt16();

                    //Debug.LogFormat("recv head {0} {1} {2}", dataLen, packLen, msgID);

                    if (packLen == MSG_ID_SIZE) //表示包体没有数据，只有一个消息ID，这时发起新的包异步读取
                    {
                        BeginPackRead();    
                    }
                    else if (packLen < MSG_ID_SIZE)
                    {
                        throw new Exception("recv pack len " + packLen); //服务器发送过来一个错误的包大小
                    }
                    else //发起异步读取包体
                    {
                        AsyncData pack_data = new AsyncData
                        {
                            buff = new byte[packLen - MSG_ID_SIZE], //计算包体大小需要减掉消息id所占的2个字节
                            pos = 0,
                            messId = msgID
                        };

                        mNetStream.BeginRead(pack_data.buff, 0, pack_data.buff.Length, new AsyncCallback(ReadAsyncCallBackPack), pack_data);
                    }
                }
                else //没读取完则继续读取包头剩余部分
                {
                    head_data.pos += dataLen;

                    Debug.LogFormat("continue recv head {0} {1}", head_data.buff.Length, head_data.pos);

                    mNetStream.BeginRead(head_data.buff, head_data.pos, head_data.buff.Length - head_data.pos, new AsyncCallback(ReadAsyncCallBackPackHead), head_data);
                }
            }
            catch (Exception ex)
            {
                U3DUtility.Loom.QueueOnMainThread(() =>
                {
                    Disconnect(ex.ToString());
                });

                return;
            }
        }

        /// <summary>
        /// 异步读取包体回调函数
        /// </summary>
        /// <param name="asyncResult">读取结果</param>
        void ReadAsyncCallBackPack(IAsyncResult asyncResult)
        {
            try
            {
                int dataLen = mNetStream.EndRead(asyncResult);

                AsyncData data = (AsyncData)asyncResult.AsyncState;

                if (data.pos + dataLen == data.buff.Length) //读取完毕后放入队列，开始读取下一个包
                {
                    Pkt p;
                    p.data = data.buff;
                    p.messId = data.messId;

                    lock(mRecvPacks)
                    {
                        //Debug.LogFormat("recv data {0} {1}", data.buff.Length, p.messId);

                        mRecvPacks.Enqueue(p);
                    }

                    BeginPackRead();
                }
                else //没读取完需要继续读取
                {
                    data.pos += dataLen;

                    //Debug.LogFormat("continue recv data {0} {1}", data.buff.Length, data.pos);

                    mNetStream.BeginRead(data.buff, data.pos, data.buff.Length - data.pos, new AsyncCallback(ReadAsyncCallBackPack), data);
                }
            }
            catch (Exception ex)
            {
                U3DUtility.Loom.QueueOnMainThread(() =>
                {
                    Disconnect(ex.ToString());
                });
            }
        }

        /// <summary>
        /// 开始异步读取包
        /// </summary>
        void BeginPackRead()
        {
            AsyncData data = new AsyncData
            {
                buff = new byte[PACK_HEAD_SIZE + MSG_ID_SIZE], //包大小加上ID一共6个字节
                pos = 0
            };

            try
            {
                mNetStream.BeginRead(data.buff, 0, data.buff.Length, new AsyncCallback(ReadAsyncCallBackPackHead), data);
            }
            catch (Exception ex)
            {
                U3DUtility.Loom.QueueOnMainThread(() =>
                {
                    Disconnect(ex.ToString());
                });
            }
        }

    }
}
