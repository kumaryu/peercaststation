using System;

namespace PeerCastStation.TS
{
  public class TSPacket
  {
    public int sync_byte { get; private set; }
    public int transport_error_indicator { get; private set; }
    public int payload_unit_start_indicator { get; private set; }
    public int transport_priority { get; private set; }
    public int PID { get; private set; }
    public int PMTID { get; private set; }
    public int transport_scrambling_control { get; private set; }
    public int adaptation_field_control { get; private set; }
    public int continuity_counter { get; private set; }
    public int adaptation_field_length { get; private set; }
    public int random_access_indicator { get; private set; }
    public bool audio_block { get; private set; }
    public bool video_block { get; private set; }
    public bool keyframe { get; private set; }

    public TSPacket(byte[] packet)
    {
      this.sync_byte = packet[0];
      this.transport_error_indicator = (packet[1] & 0x80) >> 7;
      this.payload_unit_start_indicator = (packet[1] & 0x40) >> 6;
      this.transport_priority = (packet[1] & 0x20) >> 5;
      this.PID = ((packet[1] & 0x1F) << 8) | packet[2];
      this.PMTID = 0;
      this.transport_scrambling_control = (packet[3] & 0x60) >> 6;
      this.adaptation_field_control = (packet[3] & 0x30) >> 4;
      this.continuity_counter = (packet[3] & 0x0F);
      this.adaptation_field_length = 0;
      this.random_access_indicator = 0;
      this.audio_block = false;
      this.video_block = false;
      this.keyframe = false;
      if (this.payload_unit_start_indicator > 0 && this.PID == 0)
      {
        //PAT
        int section_length = (packet[6] & 0x0F << 8 | packet[7]);
        for(int i=0;i<section_length-9;i+=4)
        {
          byte[] pmts = new byte[4];
          Array.Copy(packet, 4+1+8+i, pmts, 0, 4);
          int program_number = pmts[0] << 8 | pmts[1];
          int pmtid = (pmts[2] & 0x1F) << 8 | pmts[3];
          if(program_number>0) {
            this.PMTID = pmtid;
          }
        }
      }
      if ((this.adaptation_field_control & 0x02)!=0)
      {
        this.adaptation_field_length = packet[4];
        if (this.adaptation_field_length > 0)
        {
          this.random_access_indicator = packet[5] & 0x40;
          int i = 5 + adaptation_field_length;
          if (packet[i] == 0x0 && packet[i + 1] == 0x0 && packet[i + 2] == 0x1 && packet[i + 3] == 0xC0)
          {
            this.audio_block = true;
          }
          if (packet[i] == 0x0 && packet[i + 1] == 0x0 && packet[i + 2] == 0x1 && packet[i + 3] == 0xE0)
          {
            this.video_block = true;
          }
          if (this.video_block && this.random_access_indicator > 0)
          {
            this.keyframe = true;
          }
        }
      }
    }
  }

}