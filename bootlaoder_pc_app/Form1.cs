using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace bootlaoder_pc_app
{
    public partial class Form1 : Form
    {
        //I threw all the code in here, but I guess it could go in  a different "Class" ?
        // OOP is not my forte :)
        //but hey, if it builds, ship it! ;)
        byte[] buff;//= File.ReadAllBytes("F:\\EclipseWorkSpace\\STM32F4_bootloader_user_app\\Debug\\STM32F4_bootloader_user_app.bin");
        string dataIN;
        byte[] bytes_received = new byte[34];
        int bytes_received_count = 0;
        byte[] rx_payload = new byte[16];
        byte[] tx_payload = new byte[16];
        public const UInt32 BL_START_OF_FRAME = 0x45444459;
        public const UInt32 BL_END_OF_FRAME = 0x46414952;
        public const UInt32 BL_HEADER = 0xFEEDEDDE;
        public const UInt32 BL_STATUS_CHECK = 0x4b4b4b4b;
        public const UInt32 BL_START_UPDATE = 0xBA5EBA11;
        public const UInt32 BL_PAYLOAD = 0xDEADBEEF;
        public const UInt32 BL_UPDATE_DONE = 0xDEADDADE;
        public const UInt32 BL_ACK_FRAME = 0x45634AED;
        public const UInt32 BL_NACK_FRAME = 0x43636AEA;
        public const UInt32 PAYLOAD_LEN = 16;

        public struct frame_type
        {
            public UInt32 start_of_frame;
            public UInt32 frame_id;
            public UInt16 frame_len;
            public UInt32 crc;
            public UInt32 end_of_frame;

        }
        public frame_type rx_frame;
        public frame_type tx_frame;
        UInt16 bytes_sent = 0;
        UInt16 stopval = 0;
        bool run = false;
        int acksCount = 0;

        public Form1()
        {
            InitializeComponent();
        }


        private void btn_conenct_Click(object sender, EventArgs e)
        {
            //TODO: add drop down list for selecting available ports
            //and baud rates etc..
            //attempt connection to a serial port
            try
            {
                port.Open();
                //if conncection is succesful we should send a session ID frame to validate this session

            }
            catch (Exception ex)
            { Console.WriteLine(ex.Message); }
        }

        private void btn_send_Click(object sender, EventArgs e)
        {


        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (port.IsOpen)
                port.Close();
        }

        MemoryStream buffer = new MemoryStream();
        private void port_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {

            for (int i = 0; i < 34; i++)
            {
                bytes_received[i] = (byte)port.ReadByte();

            }
            assemble_rx_frame_from_port_bytes();
            //send the next firmware packet only if we got an ack frame back
            //TODO: at the moment all this app does is sent firmware 
            //and on all frames recevied it just tries to send firmware...
            //for testing purposes
            if (rx_frame.frame_id == BL_ACK_FRAME)
            {
                acksCount++;
                this.Invoke(new EventHandler(increment_blocks_count));
                this.Invoke(new EventHandler(sendFirmware));
            }

        }


        public void increment_blocks_count(object sender, EventArgs e)
        {
            //everytime I get an ack means an entire 16byte block of data was successfully written
            lblAcksCount.Text = acksCount.ToString();
        }
        private void sendFirmware(object sender, EventArgs e)
        {
            //TODO refactor this puppy
            //this function will send the next frame packet containing the next block of
            //firmware data
            //it should only be called once everytime we receive an ACK that previous packet was received 
            //and also on an ack from the "start update" frame sent when btnread is pressed
            // rtb.Text += dataIN.ToString();
            run = true;
            //stuff for the counter
            int numVal = Int32.Parse(lblcount.Text);
            UInt16 length = (UInt16)buff.Length;

            //load the bin into memory
            // buff = File.ReadAllBytes("F:\\EclipseWorkSpace\\STM32F4_bootloader_user_app\\Debug\\STM32F4_bootloader_user_app.bin");

            //assemble a rx_payload frame
            rx_frame.start_of_frame = 0x45444459;
            rx_frame.frame_id = 0xDEADBEEF; //rx_payload frame ID
            rx_frame.frame_len = 42422; //number of bytes in current rx_payload
            rx_frame.crc = 0xf1f2f3f4;
            rx_frame.end_of_frame = 0x46414952;
            bool short_frame = false;
            // fills array with 16 bytes from bin file 
            while (bytes_sent < buff.Length && run == true)
            {


                //check if we are close to end of bin file and should not just simply load up
                //16 bytes ebcause there may not be 16 bytes left
                if ((length - stopval) < 16)
                {
                    stopval = (UInt16)(stopval + (length - stopval));
                    short_frame = true;
                }
                else
                {
                    stopval = (UInt16)(bytes_sent + 16);
                }

                //add bytes to rx_payload buffer
                int i = 0;
                for (; bytes_sent < stopval; bytes_sent++)
                {
                    rx_payload[i++] = buff[bytes_sent];
                    lblcount.Text = bytes_sent.ToString();
                    //rtb.Text += rx_payload[i++].ToString();
                }
                //if its a short frame, the last one then fiill it with 0xff
                if (short_frame == true)
                {
                    for (; i < 16; i++)
                    {
                        rx_payload[i] = 0xff;
                    }
                }


                //send data
                if (port.IsOpen)
                {
                    //SOF uint32
                    byte[] data = BitConverter.GetBytes(rx_frame.start_of_frame);
                    port.Write(data, 0, 4);
                    // frmae id uint32
                    data = BitConverter.GetBytes(rx_frame.frame_id);
                    port.Write(data, 0, 4);

                    //frame len uint16
                    data = BitConverter.GetBytes(rx_frame.frame_len);
                    port.Write(data, 0, 2);

                    //rx_payload 16 bytes for now
                    port.Write(rx_payload, 0, i);

                    //crc uint32
                    data = BitConverter.GetBytes(rx_frame.crc);
                    port.Write(data, 0, 4);

                    //end of frame uint32
                    data = BitConverter.GetBytes(rx_frame.end_of_frame);
                    port.Write(data, 0, 4);
                }
                run = false; //makes it run once and then wait for ok
            }
            //if we are done sending firmware then send final frame letting mcu know we are done
            if (bytes_sent >= buff.Length && run == true)
            {
                MessageBox.Show("Done");
                //assemble a rx_payload frame
                rx_frame.start_of_frame = 0x45444459;
                rx_frame.frame_id = 0xDEADDADE; //end of update
                rx_frame.frame_len = 42422; //number of bytes in current rx_payload
                rx_frame.crc = 0xf1f2f3f4;
                rx_frame.end_of_frame = 0x46414952;

                //send data
                if (port.IsOpen)
                {
                    //SOF uint32
                    byte[] data = BitConverter.GetBytes(rx_frame.start_of_frame);
                    port.Write(data, 0, 4);
                    // frmae id uint32
                    data = BitConverter.GetBytes(rx_frame.frame_id);
                    port.Write(data, 0, 4);

                    //frame len uint16
                    data = BitConverter.GetBytes(rx_frame.frame_len);
                    port.Write(data, 0, 2);

                    //rx_payload 16 bytes for now
                    port.Write(rx_payload, 0, 16);

                    //crc uint32
                    data = BitConverter.GetBytes(rx_frame.crc);
                    port.Write(data, 0, 4);

                    //end of frame uint32
                    data = BitConverter.GetBytes(rx_frame.end_of_frame);
                    port.Write(data, 0, 4);
                }
                run = false;
            }


        }

        private void Form1_Load(object sender, EventArgs e)
        {
            btnFlash.Enabled = false;

        }

        private void rtb_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnFlash_Click(object sender, EventArgs e)
        {
            //send the frame to start an update
            assemble_tx_frame_from_id(BL_START_UPDATE);
            serialize_tx_frame_down_port(tx_frame);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            int numVal = Int32.Parse(lblcount.Text);
            numVal = numVal + 1;
            lblcount.Text = numVal.ToString();
        }

        private void lblAcksCount_Click(object sender, EventArgs e)
        {

        }

        //----------------------| helper functions |---------------------------

        public void serialize_tx_frame_down_port(frame_type frame)
        {
            try
            {
                if (port.IsOpen)
                {
                    //SOF uint32
                    byte[] data = BitConverter.GetBytes(frame.start_of_frame);
                    port.Write(data, 0, 4);
                    // frmae id uint32
                    data = BitConverter.GetBytes(frame.frame_id);
                    port.Write(data, 0, 4);

                    //frame len uint16
                    data = BitConverter.GetBytes(frame.frame_len);
                    port.Write(data, 0, 2);

                    //tx_payload 16 
                    port.Write(tx_payload, 0, 16);

                    //crc uint32
                    data = BitConverter.GetBytes(frame.crc);
                    port.Write(data, 0, 4);

                    //end of frame uint32
                    data = BitConverter.GetBytes(frame.end_of_frame);
                    port.Write(data, 0, 4);
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }
        public void assemble_rx_frame_from_port_bytes()
        {
            clear_rx_frame();
            rx_frame.start_of_frame = (UInt32)(bytes_received[3] << 24 | bytes_received[2] << 16 | bytes_received[1] << 8 | bytes_received[0] << 0);
            rx_frame.frame_id = (UInt32)(bytes_received[7] << 24 | bytes_received[6] << 16 | bytes_received[5] << 8 | bytes_received[4] << 0);
            rx_frame.frame_len = (UInt16)(bytes_received[9] << 8 | bytes_received[8] << 0);
            int idx = 10;
            for (int i = 0; i < 16; i++)
            {
                rx_payload[i] = bytes_received[idx++];
            }
            rx_frame.crc = (UInt32)(bytes_received[29] << 24 | bytes_received[28] << 16 | bytes_received[27] << 8 | bytes_received[26] << 0);
            rx_frame.end_of_frame = (UInt32)(bytes_received[33] << 24 | bytes_received[32] << 16 | bytes_received[31] << 8 | bytes_received[30] << 0);

        }
        public void assemble_tx_frame_from_id(UInt32 frame_id)
        {
            clear_tx_frame();
            tx_frame.start_of_frame = BL_START_OF_FRAME;
            tx_frame.frame_id = frame_id;
            tx_frame.frame_len = 65535; //TODO           
            tx_frame.crc = 0xFFFFFFFF;//TODO
            tx_frame.end_of_frame = BL_END_OF_FRAME;


        }
        public void clear_rx_frame()
        {
            rx_frame.start_of_frame = 0;
            rx_frame.frame_id = 0;
            rx_frame.frame_len = 0;
            rx_frame.crc = 0;
            rx_frame.end_of_frame = 0;
            for (int i = 0; i < PAYLOAD_LEN; i++)
            {
                rx_payload[i] = 0;
            }
        }
        public void clear_tx_frame()
        {
            tx_frame.start_of_frame = 0;
            tx_frame.frame_id = 0;
            tx_frame.frame_len = 0;
            tx_frame.crc = 0;
            tx_frame.end_of_frame = 0;
            for (int i = 0; i < PAYLOAD_LEN; i++)
            {
                tx_payload[i] = 0;
            }
        }

        private void fileBtn_Click(object sender, EventArgs e)
        {
            filebox.ShowDialog();
           // MessageBox.Show(filebox.FileName.ToString());
            txtFilePath.Text = filebox.FileName.ToString();
            buff = File.ReadAllBytes(filebox.FileName.ToString());
            btnFlash.Enabled = true;
        }
    }
}
