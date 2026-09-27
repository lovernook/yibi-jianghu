using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Yibi.Rules;

namespace Yibi.Networking
{
    public sealed class NetSession:MonoBehaviour
    {
        struct Received {public int epoch;public WireMessage message;}
        public static NetSession Instance{get;private set;}
        public BattleState State{get;private set;}
        public int Seat{get;private set;}=-1;
        public string Room{get;private set;}
        public string Status{get;private set;}="未连接";
        public string[] LastEvents{get;private set;}=Array.Empty<string>();
        public double Deadline{get;private set;}
        public bool Pending{get;private set;}
        public bool Connected{get;private set;}
        public bool Connecting{get;private set;}
        public bool Recovering{get;private set;}
        public bool Aborted{get;private set;}
        public bool Synchronized{get;private set;}
        public bool Leaving{get;private set;}
        public bool CanAct=>Connected&&Synchronized&&!Pending&&!Leaving&&!Recovering&&Seat>=0;
        public bool[] Readiness{get;private set;}=new bool[2];
        public bool[] PeersConnected{get;private set;}=new bool[2];
        public bool[] RematchVotes{get;private set;}=new bool[2];
        public int Players{get;private set;}
        public double RecoveryRemaining=>Recovering?Math.Max(0,recoverUntil-Time.unscaledTimeAsDouble):0;
        public string LastCommandStatus{get;private set;}
        public string[] LocalLoadout{get;private set;}=new[]{"dianxue","lieshi","huifeng"};
        public string LocalMindset{get;private set;}="shouzhuo";
        int latencyMilliseconds;
        public int LatencyMilliseconds{get=>latencyMilliseconds;set{latencyMilliseconds=Mathf.Clamp(value,0,1000);transport?.SetLatency(latencyMilliseconds);}}
        // Explicit diagnostic injection, never enabled by the ordinary game UI.
        [NonSerialized] public bool DropNextActionResponseForTest;
        public static int WorkerCount=>Volatile.Read(ref ClientTransport.Workers);
        string token,host="127.0.0.1",contentHash,pendingCommand,connectionAction;int port=7777;
        ClientTransport transport;readonly ConcurrentQueue<Received> incoming=new ConcurrentQueue<Received>();
        double lastPing,lastReceived,nextRetry,recoverUntil,leaveUntil,pendingSince;int generation,queued;
        public static NetSession Ensure(){if(Instance==null)new GameObject("NetworkSession").AddComponent<NetSession>();return Instance;}
        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);contentHash=ContentCatalog.InstalledHash??ContentCatalog.Default().Hash();}
        public void Connect(string address,string action,string room=null)
        {
            if(Connecting||Leaving){Status="正在处理连接，请稍候";return;}
            if(Connected){Status="请先离开当前连接";return;}
            string error;if(!TryAddress(address,out host,out port,out error)){Status=error;return;}
            if(action!="ResumeSession"){Aborted=false;Recovering=false;ClearRoom();}
            int epoch=++generation;transport?.Stop();Connecting=true;Synchronized=false;connectionAction=action;Status=action=="ResumeSession"?"正在恢复会话…":"连接中…";
            var hello=Make(action);hello.payload.room=room;hello.payload.token=token;hello.payload.commandId=pendingCommand;
            transport=new ClientTransport(host,port,hello,m=>{if(epoch!=Volatile.Read(ref generation))return;if(Interlocked.Increment(ref queued)>64){Interlocked.Decrement(ref queued);transport?.Stop();return;}incoming.Enqueue(new Received{epoch=epoch,message=m});},Mathf.Clamp(LatencyMilliseconds,0,1000));
        }
        public static bool TryAddress(string address,out string host,out int port,out string error)
        {
            host=(address??"").Trim();port=7777;error=null;if(host.Length==0||host.Length>253||host.Contains("/")||host.Contains(" ")){error="请输入服务电脑的 IP 或主机名，可附加 :端口。";return false;}
            int colon=host.LastIndexOf(':');if(colon>=0){if(host.IndexOf(':')!=colon||!int.TryParse(host.Substring(colon+1),out port)||port<1024||port>65535){error="地址格式为 IP:端口，端口范围 1024—65535。";return false;}host=host.Substring(0,colon);}if(host.Length==0){error="服务器地址不能为空";return false;}return true;
        }
        WireMessage Make(string type)=>new WireMessage{type=type,requestId=Guid.NewGuid().ToString("N"),matchId=State?.matchId,turnId=State?.turnId??0,payload=new WirePayload{contentHash=contentHash}};
        bool SendMessage(WireMessage m){if(!Connected||transport==null||!transport.Send(m)){if(!Leaving)Lost("发送失败");return false;}return true;}
        public void Send(string type,Action<WirePayload> fill=null){var m=Make(type);fill?.Invoke(m.payload);if(type=="SelectLoadout"&&m.payload.loadout!=null){LocalLoadout=(string[])m.payload.loadout.Clone();LocalMindset=m.payload.mindset;}SendMessage(m);}
        public void Submit(string skill,QuantizedPoint[] points,bool bounds,bool overflow)
        {
            if(!CanAct||State==null||State.winner!=-2||State.activeSeat!=Seat)return;
            var m=Make("SubmitAction");m.payload.skillId=skill;m.payload.points=points??Array.Empty<QuantizedPoint>();m.payload.outOfBounds=bounds;m.payload.overflow=overflow;
            pendingCommand=m.requestId;Pending=true;pendingSince=Time.unscaledTimeAsDouble;if(!SendMessage(m))Pending=false;
        }
        public void Resume(){if(!Connected&&!Connecting&&!Leaving&&!string.IsNullOrEmpty(token)&&RecoveryRemaining>0)Connect(host+":"+port,"ResumeSession");}
        public void Rematch(){if(Connected&&State!=null&&State.winner!=-2&&Seat>=0)Send("Rematch",p=>p.ready=!RematchVotes[Seat]);}
        public void ReturnToRoom(){if(Connected&&Seat>=0&&(State==null||State.winner!=-2))Send("ReturnToRoom");}
        public void Leave()
        {
            Recovering=false;Aborted=false;if(Connected&&Seat>=0){Send("LeaveRoom");Leaving=true;leaveUntil=Time.unscaledTimeAsDouble+2;}
            else Disconnect();ClearRoom();Status="已离开房间";
        }
        void ClearRoom(){State=null;Seat=-1;token=null;Room=null;Pending=false;pendingCommand=null;Players=0;Synchronized=false;Readiness=new bool[2];PeersConnected=new bool[2];RematchVotes=new bool[2];LastEvents=Array.Empty<string>();}
        public void Disconnect(){generation++;transport?.Stop();transport=null;Connected=false;Connecting=false;Synchronized=false;Pending=false;Leaving=false;while(incoming.TryDequeue(out _))Interlocked.Decrement(ref queued);}
        // Fault injection uses exactly the ordinary lost-connection recovery path.
        public void SimulateConnectionLoss(){transport?.Stop();Lost("测试断连");}
        void Lost(string reason)
        {
            Connected=false;Connecting=false;Synchronized=false;Pending=false;transport?.Stop();
            if(Leaving){Disconnect();return;}
            if(!string.IsNullOrEmpty(token)){if(!Recovering){Recovering=true;recoverUntil=Time.unscaledTimeAsDouble+30;}nextRetry=Time.unscaledTimeAsDouble+2;Status="连接中断 · 自动恢复中，对局计时继续";}
            else Status="连接失败（"+reason+"），请检查服务地址与端口。";
        }
        void Abort(string reason){Disconnect();ClearRoom();Recovering=false;Aborted=true;Status=reason;}
        void OnDestroy(){Disconnect();if(Instance==this)Instance=null;}
        void Update()
        {
            int count=0;while(count++<32&&incoming.TryDequeue(out var received)){
                Interlocked.Decrement(ref queued);if(received.epoch!=generation)continue;var m=received.message;if(m==null||m.payload==null)continue;if(m.protocolVersion!=2){Abort("联机协议版本不一致，请更新客户端与服务。");continue;}
                lastReceived=Time.unscaledTimeAsDouble;
                if(Leaving&&m.type!="LeftRoom"&&m.type!="Disconnected")continue;
                if(DropNextActionResponseForTest&&m.requestId==pendingCommand&&(m.type=="ActionResolved"||m.type=="MatchEnded")){DropNextActionResponseForTest=false;Lost("测试丢失已结算回包");continue;}
                switch(m.type){
                    case "Connected":Connected=true;Connecting=false;lastPing=Time.unscaledTimeAsDouble;Status="已连接服务，等待会话确认";break;
                    case "Disconnected":Lost(m.payload.error);break;
                    case "Session":token=m.payload.token;Seat=m.payload.seat;Room=m.payload.room;if(m.payload.loadout!=null&&m.payload.loadout.Length==3){LocalLoadout=m.payload.loadout;LocalMindset=m.payload.mindset;}Status="已进入房间 "+Room;break;
                    case "LeftRoom":Disconnect();Status="已离开房间";break;
                    case "RoomReset":State=null;Pending=false;pendingCommand=null;LastEvents=Array.Empty<string>();Synchronized=true;ApplyRoom(m);break;
                    case "RoomState":if(Seat>=0)ApplyRoom(m);break;
                    case "MatchAborted":Abort(m.payload.error??"服务中止，本局不记胜负");break;
                    case "ActionRejected":
                        if(m.payload.errorCode=="SessionExpired"||m.payload.errorCode=="ContentMismatch"||m.payload.errorCode=="Protocol"){Abort(m.payload.error);break;}
                        if(m.requestId==pendingCommand){Pending=false;pendingCommand=null;}Status=m.payload.error;break;
                    case "MatchStarted":case "ActionResolved":case "StateSnapshot":case "MatchEnded":ApplySnapshot(m);break;
                }
            }
            double now=Time.unscaledTimeAsDouble;
            if(Leaving&&now>=leaveUntil){Disconnect();return;}
            if(Connected&&now-lastPing>=5){lastPing=now;Send("Ping");}
            if(Connected&&now-lastReceived>15)Lost("服务无响应");
            if(Recovering){if(now>=recoverUntil)Abort("恢复期限已过，本局中止；返回房间重新连接，不记录本地胜负。");else if(!Connected&&!Connecting&&now>=nextRetry)Resume();}
            if(Pending&&Connected&&now-pendingSince>4){pendingSince=now;Send("RequestSnapshot",p=>p.commandId=pendingCommand);}
        }
        void ApplyRoom(WireMessage m){Room=m.payload.room;Players=m.payload.players;Readiness=m.payload.readiness??new bool[2];PeersConnected=m.payload.connected??new bool[2];RematchVotes=m.payload.rematch??new bool[2];Status="房间 "+Room+" · "+Players+" / 2 人";}
        void ApplySnapshot(WireMessage m)
        {
            if(Seat<0||m.payload.room!=Room)return;var next=m.payload.state;
            // Only a new-match event or a handshake snapshot may establish another match identity.
            if(next!=null&&State!=null&&next.matchId!=State.matchId&&m.type!="MatchStarted"&&!(m.type=="StateSnapshot"&&!Synchronized))return;
            if(next!=null&&State!=null&&next.matchId==State.matchId&&next.revision<State.revision)return;
            if(m.type=="StateSnapshot"||m.type=="MatchStarted"){Synchronized=true;Recovering=false;Aborted=false;}
            bool changed=next==null||State==null||next.matchId!=State.matchId||next.revision>State.revision;
            if(changed)State=next;Deadline=Time.unscaledTimeAsDouble+m.payload.remaining;
            LastEvents=changed?(m.payload.events??Array.Empty<string>()):Array.Empty<string>();
            if(m.type=="StateSnapshot"||m.type=="MatchStarted"||m.requestId==pendingCommand){Pending=false;LastCommandStatus=m.payload.commandStatus;pendingCommand=null;}
            Status=next==null?"房间 "+Room+" · 等待双方准备":next.winner==-2?"权威快照 #"+next.revision:"对局结束 · 可邀请再战或回房间调整";
        }
    }
}
