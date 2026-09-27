using System;
using System.IO;
using System.Text;
using Yibi.Rules;
namespace Yibi.Networking
{
    [Serializable] public sealed class WireMessage
    {
        public int protocolVersion=2,turnId;
        public string type,requestId,matchId;
        public WirePayload payload=new WirePayload();
    }
    [Serializable] public sealed class WirePayload
    {
        public string room,token,contentHash,skillId,mindset,error,errorCode,phase,commandId,commandStatus;
        public string[] loadout,events;
        public QuantizedPoint[] points;
        public BattleState state;
        public int seat=-1,players;
        public bool ready,outOfBounds,overflow;
        public bool[] readiness,connected,rematch;
        public double remaining,recoverySeconds;
        public double[] recoveries;
    }
    public static class TcpFraming
    {
        public static byte[] Encode(string json)
        {
            var bytes=Encoding.UTF8.GetBytes(json);if(bytes.Length<1||bytes.Length>65536)throw new InvalidDataException("Frame length");
            var frame=new byte[bytes.Length+4];int n=bytes.Length;frame[0]=(byte)(n>>24);frame[1]=(byte)(n>>16);frame[2]=(byte)(n>>8);frame[3]=(byte)n;Buffer.BlockCopy(bytes,0,frame,4,n);return frame;
        }
        public static string Read(Stream stream)
        {
            var header=new byte[4];Fill(stream,header);long n=((long)header[0]<<24)|((long)header[1]<<16)|((long)header[2]<<8)|header[3];if(n<1||n>65536)throw new InvalidDataException("Frame length");
            var bytes=new byte[(int)n];Fill(stream,bytes);return new UTF8Encoding(false,true).GetString(bytes);
        }
        private static void Fill(Stream stream,byte[] bytes){int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n<=0)throw new EndOfStreamException();offset+=n;}}
    }
}
