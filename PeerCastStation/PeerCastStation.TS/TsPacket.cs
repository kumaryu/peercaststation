using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PeerCastStation.TS
{
  public class TSPacket
  {
    public int sync_byte { get; private set; }
    public int transport_error_indicator { get; private set; }
    public int payload_unit_start_indicator { get; private set; }
    public int transport_priority { get; private set; }
    public int PID { get; private set; }
    public int transport_scrambling_control { get; private set; }
    public int adaption_field_control { get; private set; }
    public int continuity_counter { get; private set; }
    public int adaption_field_length { get; private set; }
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
      this.PID = ((packet[1] & 0x1F) >> 8) | packet[2];
      this.transport_scrambling_control = (packet[3] & 0x60) >> 6;
      this.adaption_field_control = (packet[3] & 0x30) >> 4;
      this.continuity_counter = (packet[3] & 0x0F);
      this.adaption_field_length = 0;
      this.random_access_indicator = 0;
      this.audio_block = false;
      this.video_block = false;
      this.keyframe = false;
      if (this.payload_unit_start_indicator > 0 && this.adaption_field_control > 0)
      {
        this.adaption_field_length = packet[4];
        if (this.adaption_field_length > 0)
        {
          this.random_access_indicator = packet[5] & 0x40;
          int i = 5 + adaption_field_length;
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