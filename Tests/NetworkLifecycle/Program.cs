using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Yibi.Networking;
using Yibi.Rules;

var catalog=JsonSerializer.Deserialize<ContentCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ContentCatalog.json")),Peer.Json);catalog.Install();Peer.Hash=catalog.Hash();
int passed=0;void Check(bool test,string name){if(!test)throw new Exception(name);Console.WriteLine("PASS "+(++passed)+" "+name);}
async Task<Peer> Create(){var p=new Peer();p.Send("CreateRoom");await p.Wait("Session");await p.Wait("StateSnapshot");return p;}
async Task<Peer> Join(Peer a){var p=new Peer();p.Send("JoinRoom",m=>m.payload.room=a.Room);await p.Wait("Session");await p.Wait("StateSnapshot");return p;}
async Task<BattleState> Start(Peer a,Peer b){a.Send("Ready",m=>m.payload.ready=true);b.Send("Ready",m=>m.payload.ready=true);var s=(await a.Wait("MatchStarted")).payload.state;await b.Wait("MatchStarted");return s;}
async Task Reject(Peer p,WireMessage m,string code=null){p.Send(m);var rejected=await p.Wait("ActionRejected");Check(code==null||rejected.payload.errorCode==code,"reject "+(code??rejected.payload.error));}
WireMessage Action(BattleState s,string skill="basic"){var m=Peer.Make("SubmitAction");m.matchId=s.matchId;m.turnId=s.turnId;m.payload.skillId=skill;m.payload.points=Array.Empty<QuantizedPoint>();return m;}

using(var wrong=new Peer()){var m=Peer.Make("CreateRoom");m.payload.contentHash="wrong";await Reject(wrong,m,"ContentMismatch");}
using(var oldProtocol=new Peer()){var m=Peer.Make("CreateRoom");m.protocolVersion=1;await Reject(oldProtocol,m,"Protocol");}
using var a=await Create();using var b=await Join(a);using var c=await Create();using var d=await Join(c);Check(a.Room!=c.Room,"independent rooms and seats");
using(var bad=new Peer()){var m=Peer.Make("ResumeSession");m.payload.token=new string('0',48);await Reject(bad,m,"SessionExpired");}
var state=await Start(a,b);Check(c.State==null&&d.State==null,"starting one room cannot start another");
var actor=state.activeSeat==0?a:b;var other=state.activeSeat==0?b:a;var first=Action(state);await Reject(other,first);
var illegal=Action(state,"dianxue");illegal.payload.points=new[]{new QuantizedPoint(-1,4)};await Reject(actor,illegal);
actor.Send(first);state=(await actor.Wait("ActionResolved")).payload.state;await other.Wait("ActionResolved");Check(state.revision==1,"one authoritative settlement");
actor.Send(first);Check((await actor.Wait("ActionResolved")).payload.state.revision==1,"duplicate id cannot deal damage twice");
var old=Action(state);old.turnId=1;await Reject(actor,old);
string token=actor.Token;int seat=actor.Seat;actor.Dispose();await Task.Delay(200);using var resumed=new Peer();resumed.Send("ResumeSession",m=>{m.payload.token=token;m.payload.commandId=first.requestId;});await resumed.Wait("Session");var snapshot=await resumed.Wait("StateSnapshot");Check(snapshot.payload.state.revision==1&&snapshot.payload.commandStatus=="Accepted","resume answers unacknowledged command without resending");
Peer left=seat==0?resumed:other,right=seat==1?resumed:other;
while(state.winner==-2){await Task.Delay(150);var active=state.activeSeat==0?left:right;active.Send(Action(state));var response=await active.WaitAny(new[]{"ActionResolved","MatchEnded"});state=response.payload.state;await (active==left?right:left).WaitAny(new[]{"ActionResolved","MatchEnded"});}
Check(state.revision>1,"full match after recovery");
left.Send("Rematch",m=>m.payload.ready=true);await Task.Delay(250);Check(left.State.matchId==state.matchId,"one vote does not start rematch");right.Send("Rematch",m=>m.payload.ready=true);var next=(await left.Wait("MatchStarted")).payload.state;await right.Wait("MatchStarted");Check(next.matchId!=state.matchId&&next.revision==0,"same-room rematch starts clean match");await Reject(left,first);
await Reject(left,Peer.Make("ReturnToRoom"));left.Send("LeaveRoom");Check((await right.Wait("MatchEnded")).payload.state.winner==1,"explicit leave immediately forfeits");await left.Wait("LeftRoom");right.Send("ReturnToRoom");await right.Wait("RoomReset");using(var replacement=await Join(right)){Check(replacement.Seat==0,"remaining seat can accept replacement player");replacement.Send("LeaveRoom");await replacement.Wait("LeftRoom");}
right.Send("LeaveRoom");await right.Wait("LeftRoom");

using(var oversized=new TcpClient("127.0.0.1",7777)){oversized.ReceiveTimeout=5000;oversized.GetStream().Write(new byte[]{0,1,0,1});Check(oversized.GetStream().ReadByte()==-1,"oversized frame disconnects safely");}
using(var flood=new Peer()){try{for(int i=0;i<20;i++)flood.Send("Ping");}catch(IOException){}await flood.Closed.WaitAsync(TimeSpan.FromSeconds(5));Check(true,"rate-limit closes flooded transport");}

var timeoutState=await Start(c,d);Console.WriteLine("Waiting for real 26-second turn deadlines; heartbeat remains active.");
var firstTimeout=await c.Wait("ActionResolved",60);await d.Wait("ActionResolved",60);Check(firstTimeout.payload.state.fighters[timeoutState.activeSeat].timeouts==1,"first real timeout rests without immediate forfeit");
await c.Wait("ActionResolved",40);await d.Wait("ActionResolved",40);var ended=await c.Wait("MatchEnded",40);await d.Wait("MatchEnded",40);Check(ended.payload.state.winner==1-timeoutState.activeSeat,"two personal timeouts forfeit exactly once");c.Send("LeaveRoom");d.Send("LeaveRoom");await c.Wait("LeftRoom");await d.Wait("LeftRoom");
using(var e=await Create())using(var f=await Join(e)){await Start(e,f);var expiredToken=f.Token;f.Dispose();var disconnected=await e.Wait("MatchEnded",40);Check(disconnected.payload.state.winner==0,"30-second disconnected session forfeits");using(var late=new Peer()){var m=Peer.Make("ResumeSession");m.payload.token=expiredToken;await Reject(late,m,"SessionExpired");}e.Send("LeaveRoom");await e.Wait("LeftRoom");}
Console.WriteLine("NETWORK_LIFECYCLE ALL PASS count="+passed);

sealed class Peer:IDisposable
{
    public static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};public static string Hash;
    readonly TcpClient socket=new TcpClient("127.0.0.1",7777);readonly object sendLock=new object();readonly Channel<WireMessage> received=Channel.CreateUnbounded<WireMessage>();readonly CancellationTokenSource stop=new CancellationTokenSource();
    readonly Task reader;public Task Closed=>reader;public string Room,Token;public int Seat;public BattleState State;
    public Peer(){socket.NoDelay=true;reader=Task.Run(()=>{try{while(!stop.IsCancellationRequested){var m=JsonSerializer.Deserialize<WireMessage>(TcpFraming.Read(socket.GetStream()),Json);if(m.type=="Session"){Token=m.payload.token;Room=m.payload.room;Seat=m.payload.seat;}if(m.payload.state!=null)State=m.payload.state;if(m.type=="RoomReset")State=null;received.Writer.TryWrite(m);}}catch(Exception){}finally{received.Writer.TryComplete();}});_=Task.Run(async()=>{try{while(!stop.IsCancellationRequested){await Task.Delay(4000,stop.Token);Send("Ping");}}catch(Exception){}});}
    public static WireMessage Make(string type)=>new WireMessage{type=type,requestId=Guid.NewGuid().ToString("N"),payload=new WirePayload{contentHash=Hash}};
    public void Send(string type,Action<WireMessage> fill=null){var m=Make(type);fill?.Invoke(m);Send(m);}
    public void Send(WireMessage m){lock(sendLock){var bytes=TcpFraming.Encode(JsonSerializer.Serialize(m,Json));socket.GetStream().Write(bytes);}}
    public Task<WireMessage> Wait(string type,int seconds=8)=>WaitAny(new[]{type},seconds);
    public async Task<WireMessage> WaitAny(string[] types,int seconds=8){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));while(true){var m=await received.Reader.ReadAsync(timeout.Token);if(types.Contains(m.type))return m;if(m.type=="ActionRejected")throw new Exception("Unexpected rejection: "+m.payload.error);}}
    public void Dispose(){if(stop.IsCancellationRequested)return;stop.Cancel();socket.Close();}
}
