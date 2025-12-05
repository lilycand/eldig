using System;
using System.Threading;

namespace HemodialysisFSM
{
    // --- DEFINISI STATE (S0 - S8) ---
    public enum MachineState
    {
        S0_IDLE,
        S1_NORMAL,
        S2_FLOW_WARN,
        S3_TEMP_WARN,
        S4_O2_WARN,
        S5_COND_WARN,
        S6_TEMP_CRIT,   // Critical
        S7_PRESS_FAIL,  // Critical
        S8_AIR_DETECT   // Critical
    }

    // --- STRUKTUR SENSOR (Input) ---
    // Menggunakan integer untuk simulasi 2-bit logic: 
    // 0 = Normal (00), 1 = Warning (10), 2 = Critical (11)
    public class SensorInputs
    {
        public int Flow_Code { get; set; } = 0; // 0:Normal, 1:Warn, 2:Crit
        public int Temp_Code { get; set; } = 0;
        public int Press_Code { get; set; } = 0;
        public int Cond_Code { get; set; } = 0;
        public bool SpO2_Hypoxia { get; set; } = false; // 1-bit
        public bool Air_Bubble { get; set; } = false;   // 1-bit (Fatal)
        
        public bool Start_Cmd { get; set; } = false;
        public bool Reset_Cmd { get; set; } = false;
        public bool Stop_Cmd { get; set; } = false;
    }

    // --- STRUKTUR AKTUATOR (Output) ---
    public class ActuatorOutputs
    {
        public bool Pump_On { get; set; } = false;
        public bool Safety_Clamp_Closed { get; set; } = false; // True = Nutup (Bahaya)
        public string Peltier_Status { get; set; } = "OFF";
        public string Alarm_Status { get; set; } = "SILENT";
        public string Info_Message { get; set; } = "System Ready";
    }

    // --- KELAS UTAMA FSM ---
    public class HemodialysisController
    {
        private MachineState currentState = MachineState.S0_IDLE;
        public SensorInputs Sensors = new SensorInputs();
        public ActuatorOutputs Actuators = new ActuatorOutputs();

        // Fungsi Logika Utama (Dipanggil setiap siklus/detik)
        public void UpdateLogic()
        {
            // 1. Reset Logic (Jika tombol reset ditekan saat Critical)
            if (Sensors.Reset_Cmd)
            {
                currentState = MachineState.S0_IDLE;
                ResetSensors();
                Actuators.Info_Message = "SYSTEM RESET. Going to IDLE.";
                return;
            }

            // 2. Safety Latch Logic (Jika sudah Critical, KUNCI di situ)
            if (IsCriticalState(currentState))
            {
                // Tetap di state kritis, jangan berubah meskipun sensor sudah normal
                // Hanya bisa keluar lewat Reset (logika di atas)
                SetOutputs(); 
                return; 
            }

            // 3. Priority 1: SAFETY OVERRIDE (Deteksi Bahaya Fatal)
            // Jika mendeteksi Udara atau Tekanan Fatal, langsung lompat ke Critical
            if (Sensors.Air_Bubble)
            {
                currentState = MachineState.S8_AIR_DETECT;
            }
            else if (Sensors.Press_Code == 2) // Code 11 (Critical)
            {
                currentState = MachineState.S7_PRESS_FAIL;
            }
            else if (Sensors.Temp_Code == 2) // Code 11 (Hyperthermia)
            {
                currentState = MachineState.S6_TEMP_CRIT;
            }
            
            // 4. Priority 2: Normal Operations & Warnings
            else
            {
                switch (currentState)
                {
                    case MachineState.S0_IDLE:
                        if (Sensors.Start_Cmd) currentState = MachineState.S1_NORMAL;
                        break;

                    case MachineState.S1_NORMAL:
                    case MachineState.S2_FLOW_WARN:
                    case MachineState.S3_TEMP_WARN:
                    case MachineState.S4_O2_WARN:
                    case MachineState.S5_COND_WARN:
                        // Evaluasi kondisi Warning (Non-Fatal)
                        if (Sensors.Stop_Cmd) currentState = MachineState.S0_IDLE;
                        else if (Sensors.Flow_Code == 1) currentState = MachineState.S2_FLOW_WARN;
                        else if (Sensors.Temp_Code == 1) currentState = MachineState.S3_TEMP_WARN;
                        else if (Sensors.SpO2_Hypoxia)   currentState = MachineState.S4_O2_WARN;
                        else if (Sensors.Cond_Code == 1) currentState = MachineState.S5_COND_WARN;
                        else currentState = MachineState.S1_NORMAL; // Kembali normal jika tidak ada warning
                        break;
                }
            }

            // Update Output Aktuator berdasarkan State terbaru
            SetOutputs();
        }

        private void SetOutputs()
        {
            // Default: Aman
            Actuators.Pump_On = false;
            Actuators.Safety_Clamp_Closed = false;
            Actuators.Peltier_Status = "OFF";
            Actuators.Alarm_Status = "SILENT";

            switch (currentState)
            {
                case MachineState.S0_IDLE:
                    Actuators.Info_Message = "Standby. Waiting for Start...";
                    break;

                case MachineState.S1_NORMAL:
                    Actuators.Pump_On = true;
                    Actuators.Info_Message = "Therapy Running Normally.";
                    break;

                // --- WARNING STATES (Pompa Tetap Jalan, Ada Koreksi) ---
                case MachineState.S2_FLOW_WARN:
                    Actuators.Pump_On = true;
                    Actuators.Alarm_Status = "BEEP (Warning)";
                    Actuators.Info_Message = "Adjusting Pump Speed...";
                    break;
                case MachineState.S3_TEMP_WARN:
                    Actuators.Pump_On = true;
                    Actuators.Peltier_Status = "ADJUSTING";
                    Actuators.Alarm_Status = "BEEP (Warning)";
                    Actuators.Info_Message = "Stabilizing Temperature...";
                    break;
                case MachineState.S4_O2_WARN:
                    Actuators.Pump_On = true;
                    Actuators.Alarm_Status = "BEEP (Warning)";
                    Actuators.Info_Message = "Increasing Oxygen Flow...";
                    break;
                case MachineState.S5_COND_WARN:
                    Actuators.Pump_On = true;
                    Actuators.Alarm_Status = "BEEP (Warning)";
                    Actuators.Info_Message = "Correcting Dialysate Mix...";
                    break;

                // --- CRITICAL STATES (E-STOP) ---
                case MachineState.S6_TEMP_CRIT:
                    Actuators.Pump_On = false;       // Stop
                    Actuators.Safety_Clamp_Closed = true; // Tutup
                    Actuators.Peltier_Status = "OFF (Safety)";
                    Actuators.Alarm_Status = "SIREN (CRITICAL)";
                    Actuators.Info_Message = "DANGER! Temp Critical. System Locked.";
                    break;
                case MachineState.S7_PRESS_FAIL:
                    Actuators.Pump_On = false;
                    Actuators.Safety_Clamp_Closed = true;
                    Actuators.Alarm_Status = "SIREN (CRITICAL)";
                    Actuators.Info_Message = "DANGER! Pressure Occlusion. System Locked.";
                    break;
                case MachineState.S8_AIR_DETECT:
                    Actuators.Pump_On = false;
                    Actuators.Safety_Clamp_Closed = true;
                    Actuators.Alarm_Status = "SIREN (CRITICAL)";
                    Actuators.Info_Message = "EMERGENCY! Air Bubble Detected. System Locked.";
                    break;
            }
        }

        private bool IsCriticalState(MachineState s)
        {
            return s == MachineState.S6_TEMP_CRIT || 
                   s == MachineState.S7_PRESS_FAIL || 
                   s == MachineState.S8_AIR_DETECT;
        }

        public void ResetSensors()
        {
            Sensors.Air_Bubble = false;
            Sensors.Press_Code = 0;
            Sensors.Temp_Code = 0;
            Sensors.Flow_Code = 0;
            Sensors.Cond_Code = 0;
            Sensors.SpO2_Hypoxia = false;
            Sensors.Start_Cmd = false;
            Sensors.Reset_Cmd = false;
        }

        public MachineState GetState() => currentState;
    }

    // --- PROGRAM UTAMA (UI Console) ---
    class Program
    {
        static void Main(string[] args)
        {
            HemodialysisController machine = new HemodialysisController();

            while (true)
            {
                Console.Clear();
                Console.WriteLine("====================================================");
                Console.WriteLine("   SMART HEMODIALYSIS CONTROL SYSTEM SIMULATION");
                Console.WriteLine("====================================================");
                
                // Tampilkan Status
                PrintStatus(machine);

                // Menu Input User
                Console.WriteLine("\n--- SENSOR INJECTION PANEL ---");
                Console.WriteLine("[1] START Machine");
                Console.WriteLine("[2] Simulate: AIR BUBBLE (Fatal)");
                Console.WriteLine("[3] Simulate: PRESSURE FAIL (Fatal)");
                Console.WriteLine("[4] Simulate: TEMP CRITICAL (Fatal)");
                Console.WriteLine("[5] Simulate: Temp Warning (Minor)");
                Console.WriteLine("[6] Simulate: Flow Warning (Minor)");
                Console.WriteLine("[7] Clear Sensors (Normal Condition)");
                Console.WriteLine("[9] STOP Machine");
                Console.WriteLine("[0] RESET SYSTEM (Unlock Critical)");
                Console.Write("\nInput Command > ");

                var key = Console.ReadKey(true).Key;
                
                // Reset command flags
                machine.Sensors.Start_Cmd = false;
                machine.Sensors.Stop_Cmd = false;
                machine.Sensors.Reset_Cmd = false;

                switch (key)
                {
                    case ConsoleKey.D1: machine.Sensors.Start_Cmd = true; break;
                    case ConsoleKey.D2: machine.Sensors.Air_Bubble = true; break;
                    case ConsoleKey.D3: machine.Sensors.Press_Code = 2; break; // Code 11
                    case ConsoleKey.D4: machine.Sensors.Temp_Code = 2; break;  // Code 11
                    case ConsoleKey.D5: machine.Sensors.Temp_Code = 1; break;  // Code 10
                    case ConsoleKey.D6: machine.Sensors.Flow_Code = 1; break;  // Code 10
                    case ConsoleKey.D7: machine.ResetSensors(); break;         // Back to 00
                    case ConsoleKey.D9: machine.Sensors.Stop_Cmd = true; break;
                    case ConsoleKey.D0: machine.Sensors.Reset_Cmd = true; break;
                }

                machine.UpdateLogic();
                Thread.Sleep(100); // Delay sedikit agar tidak flickering
            }
        }

        static void PrintStatus(HemodialysisController m)
        {
            // Warna Status Berdasarkan State
            if (m.GetState() == MachineState.S0_IDLE) Console.ForegroundColor = ConsoleColor.Gray;
            else if (m.GetState() == MachineState.S1_NORMAL) Console.ForegroundColor = ConsoleColor.Green;
            else if (m.GetState() >= MachineState.S6_TEMP_CRIT) Console.ForegroundColor = ConsoleColor.Red;
            else Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine($"\nCURRENT STATE : {m.GetState()}");
            Console.WriteLine($"INFO          : {m.Actuators.Info_Message}");
            Console.ResetColor();

            Console.WriteLine("\n--- ACTUATOR STATUS ---");
            Console.WriteLine($"BLOOD PUMP    : {(m.Actuators.Pump_On ? "[ON] Running" : "[OFF] Stopped")}");
            Console.WriteLine($"SAFETY CLAMP  : {(m.Actuators.Safety_Clamp_Closed ? "[CLOSED] BLOCKED" : "[OPEN] Flowing")}");
            Console.WriteLine($"PELTIER       : {m.Actuators.Peltier_Status}");
            Console.WriteLine($"ALARM         : {m.Actuators.Alarm_Status}");

            Console.WriteLine("\n--- SENSOR INPUTS ---");
            Console.WriteLine($"Air Bubble    : {m.Sensors.Air_Bubble}");
            Console.WriteLine($"Pressure Code : {m.Sensors.Press_Code} (2=Crit, 0=Norm)");
            Console.WriteLine($"Temp Code     : {m.Sensors.Temp_Code} (2=Crit, 1=Warn)");
        }
    }
}