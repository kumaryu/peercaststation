
namespace PeerCastStation.FLV.RTMP
{
	interface IRTMPContentSink
	{
		void OnFLVHeader();
		void OnData(DataMessage msg);
		void OnVideo(RTMPMessage msg);
		void OnAudio(RTMPMessage msg);
	}
}
