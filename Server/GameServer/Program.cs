using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Yibi.Rules;
using Yibi.Networking;

var host=new GameHost(args.Length>0?int.Parse(args[0]):7777);
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;host.Stop();};
await host.Run();

sealed class Peer
{
    public TcpClient socket;
    public Channel<byte[]> outgoing=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    public Room room;public int seat=-1,count;public double lastSeen,window;public bool closed;
    public void Close(){if(closed)return;closed=true;socket.Close();outgoing.Writer.TryComplete();}
}
sealed class Session
{
    public string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    public Peer peer;public double disconnected=-1;public bool ready,rematch;
    public string[] loadout={"dianxue","lieshi","huifeng"};public string mindset="shouzhuo";
}
sealed class Room
{
    public string code;public Session[] seats=new Session[2];public BattleState state;public double deadline;
    public Dictionary<string,WireMessage> cache=new Dictionary<string,WireMessage>();public Queue<string> cacheOrder=new Queue<string>();
}
sealed class GameHost
{
    readonly TcpListener listener;readonly object gate=new object();readonly Stopwatch clock=Stopwatch.StartNew();
    readonly JsonSerializerOptions json=new JsonSerializerOptions{IncludeFields=true,MaxDepth=32};
    readonly List<Peer> peers=new List<Peer>();readonly Dictionary<string,Room> rooms=new Dictionary<string,Room>();readonly List<string> expired=new List<string>();
    readonly CancellationTokenSource stopping=new CancellationTokenSource();readonly string hash;bool ending;
    const double Recovery=30,TurnSeconds=26;
    public GameHost(int port){listener=new TcpListener(IPAddress.Any,port);var path=Path.Combine(AppContext.BaseDirectory,"ContentCatalog.json");var content=JsonSerializer.Deserialize<ContentCatalog>(File.ReadAllText(path),json);content.Install();hash=content.Hash();}
    double Now=>clock.Elapsed.TotalSeconds;
    public async Task Run()
    {
        listener.Start();Console.WriteLine("YIBI_SERVER READY "+listener.LocalEndpoint+" content="+hash+" rooms=32");
        var timer=Task.Run(async()=>{try{while(!stopping.IsCancellationRequested){await Task.Delay(100,stopping.Token);lock(gate)Tick(Now);}}catch(OperationCanceledException){}});
        try{while(!stopping.IsCancellationRequested){var socket=await listener.AcceptTcpClientAsync(stopping.Token);socket.NoDelay=true;socket.ReceiveTimeout=16000;socket.SendTimeout=5000;var p=new Peer{socket=socket,lastSeen=Now};lock(gate){if(ending||peers.Count>=64){socket.Close();continue;}peers.Add(p);}_=Task.Run(()=>Receive(p));_=Task.Run(()=>SendLoop(p));}}
        catch(OperationCanceledException){}catch(SocketException) when(ending){}
        finally{listener.Stop();await timer;lock(gate)foreach(var p in peers)p.Close();}Console.WriteLine("YIBI_SERVER STOPPED");
    }
    public void Stop(){lock(gate){if(ending)return;ending=true;foreach(var room in rooms.Values){var m=Message(room,"MatchAborted");m.payload.error="服务已关闭，对局中止，不记胜负。";Broadcast(room,m);}}Task.Run(async()=>{await Task.Delay(300);stopping.Cancel();listener.Stop();});}
    void Receive(Peer p)
    {
        try{while(!p.closed){var text=TcpFraming.Read(p.socket.GetStream());var m=JsonSerializer.Deserialize<WireMessage>(text,json);
            // Ingress and timeout acquire the same lock before timestamping; neither can settle twice.
            lock(gate){if(p.closed||ending)break;double received=Now;if(received-p.window>=1){p.window=received;p.count=0;}if(++p.count>10){Disconnect(p);break;}Handle(p,m,received);}}}
        catch(Exception e){Console.WriteLine("CONNECTION_CLOSE "+e.GetType().Name);}finally{lock(gate)Disconnect(p);}
    }
    async Task SendLoop(Peer p){try{await foreach(var frame in p.outgoing.Reader.ReadAllAsync())await p.socket.GetStream().WriteAsync(frame);}catch(Exception){lock(gate)Disconnect(p);}}
    byte[] Encode(WireMessage m)=>TcpFraming.Encode(JsonSerializer.Serialize(m,json));
    void Send(Peer p,WireMessage m){if(p!=null&&!p.closed&&!p.outgoing.Writer.TryWrite(Encode(m)))Disconnect(p);}
    WireMessage Message(Room r,string type,string request=null)=>new WireMessage{type=type,requestId=request,matchId=r?.state?.matchId,turnId=r?.state?.turnId??0};
    void Reject(Peer p,string reason,string request,string code="Rejected"){var m=Message(p.room,"ActionRejected",request);m.payload.error=reason;m.payload.errorCode=code;Send(p,m);}
    void Broadcast(Room r,WireMessage m){var bytes=Encode(m);foreach(var s in r.seats)if(s?.peer!=null&&!s.peer.closed&&!s.peer.outgoing.Writer.TryWrite(bytes))Disconnect(s.peer);}
    WireMessage Snapshot(Room r,string type="StateSnapshot",string request=null,string command=null,int seat=-1)
    {
        var m=Message(r,type,request);m.payload.state=r.state;m.payload.room=r.code;m.payload.remaining=Math.Max(0,r.deadline-Now-1);m.payload.commandId=command;
        if(!string.IsNullOrEmpty(command)&&seat>=0)m.payload.commandStatus=r.cache.ContainsKey(Key(r.state?.matchId,seat,command))?"Accepted":"NotFound";return m;
    }
    void RoomState(Room r,string type="RoomState")
    {
        var m=Message(r,type);m.payload.room=r.code;m.payload.players=r.seats.Count(x=>x!=null);m.payload.phase=r.state==null?"waiting":r.state.winner==-2?"playing":"ended";
        m.payload.readiness=r.seats.Select(s=>s!=null&&s.ready).ToArray();m.payload.connected=r.seats.Select(s=>s?.peer!=null).ToArray();m.payload.rematch=r.seats.Select(s=>s!=null&&s.rematch).ToArray();m.payload.recoveries=r.seats.Select(s=>s==null||s.disconnected<0?0:Math.Max(0,Recovery-(Now-s.disconnected))).ToArray();Broadcast(r,m);
    }
    void Attach(Peer p,Room r,int seat,Session s,string command=null)
    {
        p.room=r;p.seat=seat;s.peer=p;s.disconnected=-1;var m=Message(r,"Session");m.payload.token=s.token;m.payload.seat=seat;m.payload.room=r.code;m.payload.contentHash=hash;m.payload.recoverySeconds=Recovery;m.payload.loadout=s.loadout;m.payload.mindset=s.mindset;Send(p,m);RoomState(r);Send(p,Snapshot(r,command:command,seat:seat));
    }
    bool Loadout(WirePayload d)=>d.loadout!=null&&d.loadout.Length==3&&d.loadout.Distinct().Count()==3&&d.loadout.All(x=>BattleContent.Ids.Contains(x))&&(d.mindset=="shouzhuo"||d.mindset=="fanzhao");
    void Handle(Peer p,WireMessage m,double received)
    {
        if(m==null||m.protocolVersion!=2||m.payload==null||string.IsNullOrEmpty(m.type)||m.type.Length>40||string.IsNullOrEmpty(m.requestId)||m.requestId.Length>80){Reject(p,"联机协议不一致或格式错误，请更新客户端与服务。",m?.requestId,"Protocol");return;}
        p.lastSeen=received;
        if(m.payload.contentHash!=hash){Reject(p,"内容版本不一致，请使用相同版本的客户端与服务。",m.requestId,"ContentMismatch");return;}
        if(m.type=="Ping"){Send(p,Message(p.room,"Pong",m.requestId));return;}
        if(m.type=="ResumeSession"){
            if(p.seat>=0){Reject(p,"已在房间中",m.requestId);return;}
            if(m.payload.token!=null&&m.payload.token.Length==48)foreach(var room in rooms.Values)for(int i=0;i<2;i++){var s=room.seats[i];if(s!=null&&s.token==m.payload.token&&(s.disconnected<0||received-s.disconnected<=Recovery)){if(s.peer!=null)Disconnect(s.peer);Attach(p,room,i,s,m.payload.commandId);return;}}
            Reject(p,"会话已过期或服务已重启，本局中止；可重新创建房间。",m.requestId,"SessionExpired");return;
        }
        if(m.type=="CreateRoom"){
            if(p.seat>=0||rooms.Count>=32){Reject(p,"已在房间中或服务房间已满",m.requestId);return;}string code;do{code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();}while(rooms.ContainsKey(code));var room=new Room{code=code};rooms.Add(code,room);room.seats[0]=new Session();Attach(p,room,0,room.seats[0]);Console.WriteLine("ROOM "+code+" created");return;
        }
        if(m.type=="JoinRoom"){
            Room room;if(p.seat>=0||m.payload.room==null||!rooms.TryGetValue(m.payload.room,out room)||room.state!=null||room.seats.All(s=>s!=null)){Reject(p,"房间不存在、已满或已开局",m.requestId);return;}int seat=Array.FindIndex(room.seats,s=>s==null);room.seats[seat]=new Session();Attach(p,room,seat,room.seats[seat]);return;
        }
        var r=p.room;if(r==null||p.seat<0||r.seats[p.seat]?.peer!=p){Reject(p,"尚未加入房间",m.requestId);return;}var session=r.seats[p.seat];
        if(m.type=="LeaveRoom"){
            if(r.state!=null&&r.state.winner==-2)End(r,1-p.seat,"对手主动离开，认输");
            r.seats[p.seat]=null;p.seat=-1;p.room=null;Send(p,new WireMessage{type="LeftRoom",requestId=m.requestId});RoomState(r);if(r.seats.All(s=>s==null))rooms.Remove(r.code);return;
        }
        if(m.type=="RequestSnapshot"){Send(p,Snapshot(r,request:m.requestId,command:m.payload.commandId,seat:p.seat));return;}
        if(m.type=="ReturnToRoom"){
            if(r.state!=null&&r.state.winner==-2){Reject(p,"对局进行中，离开会认输",m.requestId);return;}r.state=null;r.cache.Clear();r.cacheOrder.Clear();foreach(var s in r.seats)if(s!=null){s.ready=false;s.rematch=false;}RoomState(r,"RoomReset");return;
        }
        if(m.type=="Rematch"){
            if(r.state==null||r.state.winner==-2){Reject(p,"当前没有已结束的对局",m.requestId);return;}session.rematch=m.payload.ready;RoomState(r);if(r.seats.All(s=>s?.peer!=null&&s.rematch))StartMatch(r);return;
        }
        if(m.type=="SelectLoadout"){
            if(r.state!=null||!Loadout(m.payload)){Reject(p,"构筑无效或对局已开始",m.requestId);return;}session.loadout=(string[])m.payload.loadout.Clone();session.mindset=m.payload.mindset;session.ready=false;RoomState(r);return;
        }
        if(m.type=="Ready"){
            if(r.state!=null){Reject(p,"对局已开始",m.requestId);return;}session.ready=m.payload.ready;RoomState(r);if(r.seats.All(s=>s?.peer!=null&&s.ready))StartMatch(r);return;
        }
        if(m.type!="SubmitAction"){Reject(p,"未知消息",m.requestId);return;}
        if(r.state==null||m.matchId!=r.state.matchId){Reject(p,"对局已更换，旧行动已拒绝",m.requestId);return;}
        string key=Key(m.matchId,p.seat,m.requestId);if(r.cache.ContainsKey(key)){Send(p,Snapshot(r,"ActionResolved",m.requestId,m.requestId,p.seat));return;}
        if(m.turnId!=r.state.turnId||received>r.deadline){Reject(p,"回合或期限不匹配",m.requestId);return;}
        if(m.payload.points!=null&&m.payload.points.Length>512){Reject(p,"轨迹超过上限",m.requestId);return;}
        var result=BattleReducer.Apply(r.state,new BattleCommand{commandId=m.requestId,matchId=m.matchId,turnId=m.turnId,seat=p.seat,skillId=m.payload.skillId,points=m.payload.points,outOfBounds=m.payload.outOfBounds,overflow=m.payload.overflow});
        if(!result.Accepted){Reject(p,result.rejection,m.requestId);return;}Resolve(r,result,key,m.requestId);
    }
    static string Key(string match,int seat,string request)=>match+":"+seat+":"+request;
    void StartMatch(Room r){r.cache.Clear();r.cacheOrder.Clear();foreach(var s in r.seats){s.rematch=false;s.ready=false;}r.state=BattleReducer.Create(Guid.NewGuid().ToString("N"),RandomNumberGenerator.GetInt32(2),r.seats[0].loadout,r.seats[1].loadout,r.seats[0].mindset,r.seats[1].mindset);r.deadline=Now+TurnSeconds;Broadcast(r,Snapshot(r,"MatchStarted"));RoomState(r);Console.WriteLine("MATCH START "+r.code+" "+r.state.matchId);}
    void Resolve(Room r,BattleTransition result,string key=null,string request=null){r.state=result.state;r.deadline=Now+TurnSeconds;var m=Snapshot(r,r.state.winner==-2?"ActionResolved":"MatchEnded",request);m.payload.events=result.events.ToArray();if(key!=null){r.cache[key]=m;r.cacheOrder.Enqueue(key);while(r.cacheOrder.Count>64)r.cache.Remove(r.cacheOrder.Dequeue());}Broadcast(r,m);if(r.state.winner!=-2)RoomState(r);Console.WriteLine("STATE room="+r.code+" match="+r.state.matchId+" rev="+r.state.revision+" winner="+r.state.winner);}
    void End(Room r,int winner,string reason){r.state=r.state.Copy();r.state.winner=winner;r.state.revision++;var m=Snapshot(r,"MatchEnded");m.payload.events=new[]{reason};Broadcast(r,m);}
    void Disconnect(Peer p){if(p.closed)return;p.Close();peers.Remove(p);var r=p.room;if(r!=null&&p.seat>=0&&r.seats[p.seat]?.peer==p){r.seats[p.seat].peer=null;r.seats[p.seat].disconnected=Now;r.seats[p.seat].ready=false;r.seats[p.seat].rematch=false;RoomState(r);}}
    void Tick(double now){for(int n=peers.Count-1;n>=0;n--)if(n<peers.Count&&now-peers[n].lastSeen>15)Disconnect(peers[n]);expired.Clear();foreach(var r in rooms.Values){
        for(int i=0;i<2;i++)if(r.seats[i]!=null&&r.seats[i].disconnected>=0&&now-r.seats[i].disconnected>Recovery){if(r.state!=null&&r.state.winner==-2)End(r,1-i,"对手断线超过 30 秒，判负");r.seats[i]=null;RoomState(r);}
        if(r.seats.All(s=>s==null)){expired.Add(r.code);continue;}if(r.state!=null&&r.state.winner==-2&&now>r.deadline)Resolve(r,BattleReducer.Apply(r.state,new BattleCommand{matchId=r.state.matchId,turnId=r.state.turnId,seat=r.state.activeSeat,skillId="rest",timeout=true}));
    }foreach(var id in expired)rooms.Remove(id);}
}
