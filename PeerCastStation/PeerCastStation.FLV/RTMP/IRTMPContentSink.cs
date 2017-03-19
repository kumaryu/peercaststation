
namespace PeerCastStation.FLV.RTMP
{
	public interface IRTMPContentSink
	{
		void OnFLVHeader(FLVFileHeader header);
		void OnData(DataMessage msg);
		void OnVideo(RTMPMessage msg);
		void OnAudio(RTMPMessage msg);
	}
}
