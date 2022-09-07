using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using Brush = System.Drawing.Brush;
using Rectangle = System.Drawing.Rectangle;

namespace Serial_LCD_Control
{
    internal class Serial_LCD_Library
    {
        static SerialPort m_serialPort = new SerialPort("COM3");
        const int DISPLAY_WIDTH = 320;
        const int DISPLAY_HEIGHT = 480;
        Bitmap m_bmCurrentBackground;

        public static byte[] RGBValueToByteArray(int rgb)
        {
            List<byte> resultArray = new List<byte>();

            while (rgb != 0)
            {
                resultArray.Add(Convert.ToByte(rgb & 0xFF));
                rgb >>= 8;
            }

            while (resultArray.Count < 2) resultArray.Add(0); //Pad to two bytes

            return resultArray.ToArray(); ;
        }
        private void SendReg(uint iCmd, uint iX, uint iY, uint iEx, uint iEy)
        {
            byte[] byteBuffer = new byte[6];
            byteBuffer[0] = Convert.ToByte(iX >> 2);
            byteBuffer[1] = Convert.ToByte((((iX & 3) << 6) + (iY >> 4)));
            byteBuffer[2] = Convert.ToByte((((iY & 15) << 4) + (iEx >> 6)));
            byteBuffer[3] = Convert.ToByte((((iEx & 63) << 2) + (iEy >> 8)));
            byteBuffer[4] = Convert.ToByte((iEy & 255));
            byteBuffer[5] = Convert.ToByte(iCmd);
            m_serialPort.Write(byteBuffer, 0, 6);
        }

        public void Reset()
        {
            SendReg(101, 0, 0, 0, 0);
        }
        public void Clear()
        {
            SendReg(102, 0, 0, 0, 0);
        }

        public void ScreenOff()
        {
            SendReg(108, 0, 0, 0, 0);
        }

        public void ScreenOn()
        {
            SendReg(109, 0, 0, 0, 0);
        }

        // 0 is brightest level
        public void SetBrightness(int level)
        {
            SendReg(110, Convert.ToUInt16(level), 0, 0, 0);
        }

        public void SetBackground(string strPathToBitmap)
        {
            if (strPathToBitmap == "DefaultImage")
            {
                m_bmCurrentBackground = Resource1.cosmo_public;
            }
            else
            {
                m_bmCurrentBackground = new Bitmap(strPathToBitmap);
            }

            DisplayImage(m_bmCurrentBackground, 0, 0);
        }

        public void DisplayBitmap(string strPathToBitmap, int iX = 0, int iY = 0)
        {
            Bitmap bitmImage = new Bitmap(strPathToBitmap);
            DisplayImage(bitmImage, iX, iY);
        }

        private void DisplayImage(Bitmap image, int iX, int iY)
        {
            int image_height = image.Height;
            int image_width = image.Width;

            SendReg(197, Convert.ToUInt16(iX), Convert.ToUInt16(iY), Convert.ToUInt16(iX + image_width - 1), Convert.ToUInt16(iY + image_height - 1));

            List<byte[]> bLineBuffer = new List<byte[]>();
            byte[] bLine;

            //Draw line by line each pixel using a C Struct Packing
            for (int h = 0; h < image_height; h++)
            {
                for (int w = 0; w < image_width; w++)
                {
                    //Get each pixel and pack
                    System.Drawing.Color cPixel = image.GetPixel(w, h);
                    byte bRed = Convert.ToByte(cPixel.R >> 3);
                    byte bGreen = Convert.ToByte(cPixel.G >> 2);
                    byte bBlue = Convert.ToByte(cPixel.B >> 3);
                    int rgb = (bRed << 11) | (bGreen << 5) | bBlue;
                    byte[] subLine = RGBValueToByteArray(rgb);
                    //Build our line
                    bLineBuffer.Add(subLine);
                    //Send line once it is big enough.
                    if (bLineBuffer.Count() * 2 >= DISPLAY_WIDTH * 8)
                    {
                        bLine = bLineBuffer.Cast<byte[]>().SelectMany(a => a).ToArray();
                        m_serialPort.Write(bLine, 0, bLine.Length);

                        bLineBuffer.Clear();
                    }
                }
            }


            bLine = bLineBuffer.Cast<byte[]>().SelectMany(a => a).ToArray();
            if (bLine.Length > 0)
                m_serialPort.Write(bLine, 0, bLine.Length);

        }

        public void DisplayProgressBar(int iX, int iY, int width, int height,
            Brush bar_color,
            Brush background_color,
            int min_value = 0, int max_value = 100, int value = 50,
            bool bar_outline = true, bool background_image = false)
        {
            Bitmap bitmImage;
            //SizeF sfStringSize = new SizeF();

            //If we don't have a background image we must create a drawing surface the same size.
            if (background_image == false) bitmImage = new Bitmap(DISPLAY_WIDTH, DISPLAY_HEIGHT);
            //or else we load and assign the background image
            else
            {
                //We only need the background for our bar area.
                Rectangle ImageSize = new Rectangle(iX, iY, width, height);
                bitmImage = cropImage(m_bmCurrentBackground, ImageSize);
            }

            using (Graphics graph = Graphics.FromImage(bitmImage))
            {
                //If we didn't have a background image we must fill the background color
                if (background_image == false)
                {
                    Rectangle ImageSize = new Rectangle(0, 0, width, height);
                    graph.FillRectangle(background_color, ImageSize);
                }

                //# Draw progress bar

                int bar_filled_width = (int)Math.Round(value / (max_value - (double)min_value) * width);
                Rectangle FilledImageSize = new Rectangle(0, 0, bar_filled_width, height);
                graph.FillRectangle(bar_color, FilledImageSize);

                //TODO Finish outline, I don't use it so not important now...
                //if bar_outline:
                //    # Draw outline
                //    draw.rectangle([0, 0, width - 1, height - 1], fill = None, outline = bar_color)
                //TODO
            }

            //Crop to bar
            if(width > DISPLAY_WIDTH) width = DISPLAY_WIDTH;
            Rectangle cropRect = new Rectangle(0, 0, width, height);
            bitmImage = cropImage(bitmImage, cropRect);

            DisplayImage(bitmImage, iX, iY);
        }
        private static Bitmap cropImage(Bitmap bmpImage, Rectangle cropArea)
        {
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }

        //TODO, need to support some type of right justification or way to clear around dynamic length strText.
        public void DisplayText(string strText,
            Brush brFontColor, Brush brBackgroundColor,
            int iX = 0, int iY = 0,
            bool bBackgroundImage = false,
            string strFont = "Times New Roman",
            int iFontSize = 17,
            int iPadding = 0
            )
        {
            Bitmap bitmImage;
            SizeF sfStringSize = new SizeF();

            //If we don't have a background image we must create a drawing surface the same size.
            if (bBackgroundImage == false)
                bitmImage = new Bitmap(DISPLAY_WIDTH, DISPLAY_HEIGHT);
            //or else we load and assign the background image
            else
                bitmImage = new Bitmap(m_bmCurrentBackground);

            using (Graphics graph = Graphics.FromImage(bitmImage))
            {
                //If we don't have a background image we must fill the background color
                if (bBackgroundImage == false)
                {
                    Rectangle ImageSize = new Rectangle(0, 0, DISPLAY_WIDTH, DISPLAY_HEIGHT);
                    graph.FillRectangle(brBackgroundColor, ImageSize);
                }
                using (Font font1 = new Font(strFont, iFontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    sfStringSize = graph.MeasureString(strText, font1);
                    PointF pointF1 = new PointF(iX, iY);
                    graph.DrawString(strText, font1, brFontColor, pointF1);
                }
            }

            //Crop to Text
            int iCropWidth = (int)sfStringSize.Width + iPadding; //Width of the string.
            if(iCropWidth > DISPLAY_WIDTH) iCropWidth = DISPLAY_WIDTH;
            Rectangle cropRect = new Rectangle(iX, iY,  iCropWidth, (int)sfStringSize.Height);
            bitmImage = cropImage(bitmImage, cropRect);

            //Display Text
            DisplayImage(bitmImage, iX, iY);
        }
        public Exception OpenLCD(string strCOM)
        {

            // Setting for WowNOVA/Turing Smart Screen
            m_serialPort.PortName = strCOM;
            m_serialPort.BaudRate = 115200;
            m_serialPort.Parity = Parity.None;
            m_serialPort.DataBits = 8;
            m_serialPort.StopBits = StopBits.One;
            m_serialPort.Handshake = Handshake.None;
            m_serialPort.RtsEnable = true;
            m_serialPort.DtrEnable = false;
            m_serialPort.ReadTimeout = 1000;

            try
            {
                m_serialPort.Open();
            }
            catch (Exception ex)
            {
                return ex;                
            }
            return null;
        }

        public Exception CloseLCD()
        {
            try
            {
                m_serialPort.Close();
            }
            catch (Exception ex)
            {
                return ex;
            }
            return null;
        }

    }
}
