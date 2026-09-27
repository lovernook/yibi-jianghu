using System.Net.Sockets;
using System.Text.Json;
using Yibi.Networking;
using Yibi.Rules;

var json=new JsonSerializerOptions{IncludeFields=true};var catalog=JsonSerializer.Deserialize<ContentCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ContentCatalog.json")),json);catalog.Install();string hash=catalog.Hash();
TcpClient Client(){var c=new TcpClient("127.0.0.1",7777);c.NoDelay=true;c.ReceiveTimeout=5000;return c;}
WireMessage Make(string type)=>new WireMessage{type=type,requestId=Guid.NewGuid().ToString("N"),payload=new WirePayload{contentHash=hash}};
void Send(TcpClient c,WireMessage m){var frame=TcpFraming.Encode(JsonSerializer.Serialize(m,json));c.GetStream().Write(frame);}
WireMessage Until(TcpClient c,string type){for(int i=0;i<20;i++){var m=JsonSerializer.Deserialize<WireMessage>(TcpFraming.Read(c.GetStream()),json);if(m.type==type)return m;if(m.type=="ActionRejected"&&type!="ActionRejected")throw new Exception(m.payload.error);}throw new Exception("Missing "+type);}
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
using var wrong=Client();var bad=Make("CreateRoom");bad.payload.contentHash="wrong";Send(wrong,bad);Check(Until(wrong,"ActionRejected").payload.error.Contains("版本"),"content mismatch rejected");
using var a=Client();using var b=Client();Send(a,Make("CreateRoom"));var sa=Until(a,"Session");var join=Make("JoinRoom");join.payload.room=sa.payload.room;Send(b,join);var sb=Until(b,"Session");
foreach(var c in new[]{a,b}){var ready=Make("Ready");ready.payload.ready=true;Send(c,ready);}var start=Until(a,"MatchStarted").payload.state;Until(b,"MatchStarted");
var actor=start.activeSeat==0?a:b;var other=start.activeSeat==0?b:a;var token=start.activeSeat==0?sa.payload.token:sb.payload.token;
var action=Make("SubmitAction");action.matchId=start.matchId;action.turnId=start.turnId;action.payload.skillId="basic";action.payload.points=Array.Empty<QuantizedPoint>();Send(other,action);Check(Until(other,"ActionRejected").payload.error.Contains("轮"),"wrong seat rejected");
Send(actor,action);var resolved=Until(actor,"ActionResolved");Until(other,"ActionResolved");Check(resolved.payload.state.revision==1,"single authoritative action");
Send(actor,action);var duplicate=Until(actor,"ActionResolved");Check(duplicate.payload.state.revision==1,"duplicate command cached without double damage");
action.requestId=Guid.NewGuid().ToString("N");Send(actor,action);Check(Until(actor,"ActionRejected").payload.error.Contains("回合"),"old turn rejected");
actor.Close();Thread.Sleep(250);using var resumed=Client();var resume=Make("ResumeSession");resume.payload.token=token;Send(resumed,resume);Until(resumed,"Session");var snapshot=Until(resumed,"StateSnapshot");Check(snapshot.payload.state.revision==1&&snapshot.payload.state.matchId==start.matchId,"resume restores authoritative snapshot");
Send(resumed,Make("LeaveRoom"));Check(Until(other,"MatchEnded").payload.state.winner==1-start.activeSeat,"explicit leave forfeits");Send(other,Make("LeaveRoom"));Thread.Sleep(300);
using var malformed=Client();malformed.GetStream().Write(new byte[]{0,1,0,1});Check(malformed.GetStream().ReadByte()==-1,"oversized frame closes connection");
Console.WriteLine("NETWORK_CONTRACT ALL PASS");
