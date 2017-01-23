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
      if((adaptation_field_control & 0x02)!=0)
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
      if (this.payload_unit_start_indicator > 0)
      {
        int adaptation_size = ((adaptation_field_control & 0x02)!=0) ? 1+adaptation_field_length : 0;
        int payload_offset = (adaptation_size>0) ? 4+adaptation_size : 4+1;//4byte[header]+1byte[pointer_field]
        //PAT
        if(PID==0)
        {
          int section_length = (packet[payload_offset+1] & 0x0F << 8 | packet[payload_offset+2]);
          //section_length-5byte[transport_stream_id ... last_section_number]-4byte[CRC_32]
          for(int i=0;i<section_length-5-4;i+=4)
          {
            byte[] pmts = new byte[4];
            Array.Copy(packet, payload_offset+8+i, pmts, 0, 4);//8byte[table_id ... last_section_number]
            int program_number = pmts[0] << 8 | pmts[1];
            int pmtid = (pmts[2] & 0x1F) << 8 | pmts[3];
            if(program_number>0) {
              this.PMTID = pmtid;
            }
          }
        }
      }
    }
  }

}