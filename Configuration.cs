namespace Serial_LCD_Control
{
    internal class Configuration
    {
        //Serial Section
        public string strCOMPort { get; set; } = "COM3";
        //Startup
        public bool bStartOnLoad { get; set; } = false;
        public bool bStartMinimized { get; set; } = false;
        //Display
        public bool bDateTime { get; set; } = true;
        public string strDateTimeFormat { get; set; } = "";
        int xDateTime { get; set; } = 0;
        int yDateTime { get; set; } = 0;
        public bool bCPUPercent { get; set; } = true;
        public bool bLogicalCores { get; set; } = true;
        public double Brightness { get; set; } = 0;
        public string strBackground { get; set; } = @"DefaultImage";
    }
}
