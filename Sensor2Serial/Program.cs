using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Text;
using System.Threading;
using OpenHardwareMonitor.Hardware;

namespace Sensor
{
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    internal class Program
    {
        private static string _cpuName = "";
        private static string _gpuName = "";
        private static readonly ArrayList HdNames = new ArrayList();

        private static int _cpuIndex = -1;
        private static int _gpuIndex = -1;
        private static int _ramIndex = -1;
        private static readonly Dictionary<string, int> HdIndex = new Dictionary<string, int>();

        /* CPU */
        /* CPU Total */
        private static int _cpuLoad = -1;

        /* CPU Package */
        private static int _cpuPackageTemp = -1;

        /* CPU Package */
        private static int _cpuPackagePower = -1;

        /* RAM */
        /* Memory */
        private static int _ramRate = -1;

        /* Used Memory */
        private static int _ramUsed = -1;

        /* Available Memory */
        private static int _ramAvailable = -1;

        /* GPU */
        /* GPU Core */
        private static int _gpuLoad = -1;

        /* GPU Core */
        private static int _gpuTemp = -1;

        /* Hard drive */
        private static readonly Dictionary<string, int> HdTemp = new Dictionary<string, int>();
        
        public static void Main()
        {
            var computer = new Computer {CPUEnabled = true, RAMEnabled = true, GPUEnabled = true, HDDEnabled = true};
            computer.Open();
            computer.Accept(new Visitor());

            // 分类
            for (var i = 0; i < computer.Hardware.Length; i++)
            {
                switch (computer.Hardware[i].HardwareType)
                {
                    case HardwareType.CPU:
                        _cpuIndex = i;
                        _cpuName = computer.Hardware[i].Name;
                        for (var j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                        {
                            switch (computer.Hardware[i].Sensors[j].Name)
                            {
                                case "CPU Total":
                                    _cpuLoad = j;
                                    break;
                                case "CPU Package":
                                    switch (computer.Hardware[i].Sensors[j].SensorType)
                                    {
                                        case SensorType.Temperature:
                                            _cpuPackageTemp = j;
                                            break;
                                        case SensorType.Power:
                                            _cpuPackagePower = j;
                                            break;
                                    }

                                    break;
                            }
                        }

                        break;
                    case HardwareType.GpuAti:
                    case HardwareType.GpuNvidia:
                        _gpuIndex = i;
                        _gpuName = computer.Hardware[i].Name;
                        for (var j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                        {
                            switch (computer.Hardware[i].Sensors[j].Name)
                            {
                                case "GPU Core":
                                    switch (computer.Hardware[i].Sensors[j].SensorType)
                                    {
                                        case SensorType.Load:
                                            _gpuLoad = j;
                                            break;
                                        case SensorType.Temperature:
                                            _gpuTemp = j;
                                            break;
                                    }

                                    break;
                            }
                        }

                        break;
                    case HardwareType.RAM:
                        _ramIndex = i;
                        for (var j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                        {
                            switch (computer.Hardware[i].Sensors[j].Name)
                            {
                                case "Memory":
                                    _ramRate = j;
                                    break;
                                case "Used Memory":
                                    _ramUsed = j;
                                    break;
                                case "Available Memory":
                                    _ramAvailable = j;
                                    break;
                            }
                        }

                        break;
                    case HardwareType.HDD:
                        HdNames.Add(computer.Hardware[i].Name);
                        HdIndex[computer.Hardware[i].Name] = i;

                        for (var j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                        {
                            if (computer.Hardware[i].Sensors[j].SensorType == SensorType.Temperature)
                            {
                                HdTemp[computer.Hardware[i].Name] = j;
                            }
                        }

                        break;
                }
            }

            SerialPort serialPort = null;
            try
            {
                serialPort = new SerialPort("COM7", 1500000, Parity.None, 8, StopBits.One);  // 于设备管理器里找CH340G对应的端口
                serialPort.Open();
            }
            catch
            {
                // ignored
            }

            MsgBuilder.Builder();
            
            for (;;)
            {
                computer = new Computer {CPUEnabled = true, RAMEnabled = true, GPUEnabled = true, HDDEnabled = true};
                computer.Open();
                computer.Accept(new Visitor());

                MsgBuilder.Builder().Open();
                
                // CPU
                try
                {
                    WriteLn(_cpuName);
                    WriteLn("CPU Load:\t" + computer.Hardware[_cpuIndex].Sensors[_cpuLoad].Value + "%");
                    WriteLn("CPU Power:\t" + computer.Hardware[_cpuIndex].Sensors[_cpuPackagePower].Value + "W");
                    WriteLn("CPU Temp:\t" + computer.Hardware[_cpuIndex].Sensors[_cpuPackageTemp].Value + "°C");

                    MsgBuilder.Builder()
                        .AddValue("CPUN", _cpuName)
                        .AddValue("CPUL", computer.Hardware[_cpuIndex].Sensors[_cpuLoad].Value)
                        .AddValue("CPUP", computer.Hardware[_cpuIndex].Sensors[_cpuPackagePower].Value)
                        .AddValue("CPUT", computer.Hardware[_cpuIndex].Sensors[_cpuPackageTemp].Value);
                }
                catch 
                {
                    // ignored
                }

                // RAM
                try
                {
                    WriteLn("RAM");
                    WriteLn("RAM Util:\t" + computer.Hardware[_ramIndex].Sensors[_ramRate].Value + "%");
                    WriteLn("RAM Usage:\t" + computer.Hardware[_ramIndex].Sensors[_ramUsed].Value + "/" +
                            (computer.Hardware[_ramUsed].Sensors[_ramUsed].Value +
                             computer.Hardware[_ramIndex].Sensors[_ramAvailable].Value));
                    MsgBuilder.Builder()
                        .AddValue("RAMR", computer.Hardware[_ramIndex].Sensors[_ramRate].Value)
                        .AddValue("RAMU", computer.Hardware[_ramIndex].Sensors[_ramUsed].Value)
                        .AddValue("RAMA", computer.Hardware[_ramIndex].Sensors[_ramAvailable].Value);
                }
                catch 
                {
                    // ignored
                }


                // GPU
                try
                {
                    if (_gpuName.Length != 0)
                    {
                        WriteLn(_gpuName);
                        WriteLn("GPU Load:\t" + computer.Hardware[_gpuIndex].Sensors[_gpuLoad].Value + "%");
                        WriteLn("GPU Temp:\t" + computer.Hardware[_gpuIndex].Sensors[_gpuTemp].Value + "°C");
                        MsgBuilder.Builder()
                            .AddValue("GPUN", _gpuName)
                            .AddValue("GPUL", computer.Hardware[_gpuIndex].Sensors[_gpuLoad].Value)
                            .AddValue("GPUT", computer.Hardware[_gpuIndex].Sensors[_gpuTemp].Value);
                    }
                } catch {
                    // ignored
                }

                MsgBuilder.Builder()
                    .AddValue("HD", HdNames.Count);
                // Hard drives
                for (var j = 0; j < HdNames.Count; j++)
                {
                    try
                    {
                        WriteLn(HdNames[j] as string);
                        WriteLn("Temp:\t" + computer.Hardware[HdIndex[HdNames[j] as string]]
                                    .Sensors[HdTemp[HdNames[j] as string]].Value + "°C");
                        MsgBuilder.Builder()
                            .AddValue("HD" + j, HdNames[j] as string)
                            .AddValue("HDT" + j, computer.Hardware[HdIndex[HdNames[j] as string]].Sensors[HdTemp[HdNames[j] as string]].Value );
                    }
                    catch 
                    {
                        // ignored
                    }
                }


                try
                {
                    serialPort.WriteLine(MsgBuilder.Builder().ToString());
                }
                catch
                {
                    // ignored
                }

                Thread.Sleep(1000);
            }
            
            // ReSharper disable once FunctionNeverReturns
        }

        private class Visitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (var iHardware in hardware.SubHardware)
                {
                    iHardware.Accept(this);
                }
            }

            public void VisitSensor(ISensor sensor)
            {
            }

            public void VisitParameter(IParameter parameter)
            {
            }
        }

        private static void WriteLn(string str)
        {
            Console.WriteLine(str);
        }
        
        private class MsgBuilder
        {
            private readonly StringBuilder _stringBuilder = new StringBuilder();
            private static MsgBuilder _msgBuilder;

            public static MsgBuilder Builder()
            {
                return _msgBuilder ?? (_msgBuilder = new MsgBuilder());
            }
            
            private MsgBuilder()
            {
                
            }

            // ReSharper disable once UnusedMethodReturnValue.Local
            public MsgBuilder Open()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("<");
                return this;
            }

            public MsgBuilder AddValue(string name, float? value)
            {
                return value == null ? this : AddValue(name, value.ToString());
            }

            public MsgBuilder AddValue(string name, string value)
            {
                _stringBuilder.Append(":" + name + "=" + value + ";");
                return this;
            }

            public override string ToString()
            {
                _stringBuilder.Append(">");
                var msg = _stringBuilder.ToString();
                _stringBuilder.Clear();
                return msg;
            }
        }
        
    }
}