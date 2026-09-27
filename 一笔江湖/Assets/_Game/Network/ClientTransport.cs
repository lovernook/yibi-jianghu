using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

namespace Yibi.Networking
{
    // All worker-owned resources end with the receive loop; no worker touches Unity state.
    internal sealed class ClientTransport
    {
        readonly TcpClient socket=new TcpClient();readonly BlockingCollection<byte[]> outgoing=new BlockingCollection<byte[]>(64);
        readonly Action<WireMessage> deliver;int delayMs;Thread writer;int stopped;
        internal static int Workers;
        static readonly JsonSerializerSettings Settings=new JsonSerializerSettings{NullValueHandling=NullValueHandling.Ignore,MaxDepth=32};
        public ClientTransport(string host,int port,WireMessage hello,Action<WireMessage> receive,int delay){deliver=receive;delayMs=delay;Interlocked.Increment(ref Workers);new Thread(()=>Run(host,port,hello)){IsBackground=true,Name="Yibi receive"}.Start();}
        static byte[] Encode(WireMessage m)=>TcpFraming.Encode(JsonConvert.SerializeObject(m,Settings));
        public bool Send(WireMessage m){try{return Volatile.Read(ref stopped)==0&&outgoing.TryAdd(Encode(m));}catch(InvalidOperationException){return false;}}
        public void SetLatency(int milliseconds){Volatile.Write(ref delayMs,milliseconds);}
        public void Stop(){if(Interlocked.Exchange(ref stopped,1)!=0)return;socket.Close();try{outgoing.CompleteAdding();}catch(ObjectDisposedException){}}
        void Run(string host,int port,WireMessage hello)
        {
            try{
                var task=socket.ConnectAsync(host,port);if(!task.Wait(5000))throw new TimeoutException();task.GetAwaiter().GetResult();if(Volatile.Read(ref stopped)!=0)return;
                socket.NoDelay=true;socket.ReceiveTimeout=16000;socket.SendTimeout=5000;deliver(new WireMessage{type="Connected"});Send(hello);
                Interlocked.Increment(ref Workers);writer=new Thread(()=>{try{foreach(var bytes in outgoing.GetConsumingEnumerable()){int delay=Volatile.Read(ref delayMs);if(delay>0)Thread.Sleep(delay);socket.GetStream().Write(bytes,0,bytes.Length);}}catch(Exception){socket.Close();}finally{Interlocked.Decrement(ref Workers);}}){IsBackground=true,Name="Yibi send"};writer.Start();
                while(Volatile.Read(ref stopped)==0){var m=JsonConvert.DeserializeObject<WireMessage>(TcpFraming.Read(socket.GetStream()),Settings);int delay=Volatile.Read(ref delayMs);if(delay>0)Thread.Sleep(delay);deliver(m);}
            }catch(Exception ex){if(Volatile.Read(ref stopped)==0)deliver(new WireMessage{type="Disconnected",payload=new WirePayload{error=ex.GetType().Name}});}
            finally{Stop();if(writer==null||writer.Join(1500))outgoing.Dispose();Interlocked.Decrement(ref Workers);}
        }
    }
}
