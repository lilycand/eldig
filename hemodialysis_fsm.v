`timescale 1ns / 1ps

module tb_hemodialysis;

    // ==========================================
    // 1. DEKLARASI SINYAL (INPUT & OUTPUT)
    // ==========================================
    reg clk;
    reg reset;
    reg start_cmd;
    reg stop_cmd;
    reg flow_warn;
    reg temp_warn;
    reg o2_warn;
    reg cond_warn;
    reg temp_crit;
    reg press_fail;
    reg air_detect;

    wire pump_on;
    wire clamp_open;
    wire peltier_heat;
    wire mfc_increase;
    wire dosing_pump_correct;
    wire alarm_warning;
    wire alarm_emergency;

    // ==========================================
    // 2. MENGHUBUNGKAN KE MODUL UTAMA (UUT)
    // ==========================================
    hemodialysis_fsm uut (
        .clk(clk), 
        .reset(reset), 
        .start_cmd(start_cmd), 
        .stop_cmd(stop_cmd), 
        .flow_warn(flow_warn), 
        .temp_warn(temp_warn), 
        .o2_warn(o2_warn), 
        .cond_warn(cond_warn), 
        .temp_crit(temp_crit), 
        .press_fail(press_fail), 
        .air_detect(air_detect), 
        .pump_on(pump_on), 
        .clamp_open(clamp_open), 
        .peltier_heat(peltier_heat), 
        .mfc_increase(mfc_increase), 
        .dosing_pump_correct(dosing_pump_correct), 
        .alarm_warning(alarm_warning), 
        .alarm_emergency(alarm_emergency)
    );

    // ==========================================
    // 3. GENERASI CLOCK
    // ==========================================
    initial begin
        clk = 0;
        forever #5 clk = ~clk; // Clock berkedip setiap 5 satuan waktu
    end

    // ==========================================
    // 4. PEREKAMAN DATA GRAFIK (PENTING!)
    // ==========================================
    initial begin
        // Baris ini yang membuat grafik muncul di GTKWave
        $dumpfile("grafik_hemodialisis.vcd"); 
        $dumpvars(0, tb_hemodialysis);  
    end

    // ==========================================
    // 5. SKENARIO PENGUJIAN (STIMULUS)
    // ==========================================
    initial begin
        // Menampilkan teks status di Terminal
        $monitor("Waktu=%0t | State=%b | Pump=%b | Clamp=%b | Warning=%b | Emergency=%b", 
                 $time, uut.current_state, pump_on, clamp_open, alarm_warning, alarm_emergency);

        $display("\n--- MULAI SIMULASI ---");

        // --- SKENARIO 1: Kondisi Awal & Reset ---
        reset = 1;
        start_cmd = 0; stop_cmd = 0;
        flow_warn = 0; temp_warn = 0; o2_warn = 0; cond_warn = 0;
        temp_crit = 0; press_fail = 0; air_detect = 0;
        #10;
        reset = 0; 

        // --- SKENARIO 2: Menyalakan Mesin (Start) ---
        #10;
        $display("\n[TEST] Tombol Start Ditekan -> Masuk Mode NORMAL");
        start_cmd = 1;
        #10 start_cmd = 0; 

        // --- SKENARIO 3: Simulasi Warning (Suhu Naik) ---
        #20;
        $display("\n[TEST] Peringatan Suhu (Warning) -> Heater Aktif");
        temp_warn = 1;
        #30; 
        temp_warn = 0; // Suhu kembali normal
        $display("[TEST] Suhu Kembali Normal");

        // --- SKENARIO 4: Simulasi Bahaya (Gelembung Udara) ---
        #20;
        $display("\n[TEST] BAHAYA! Ada Gelembung Udara -> E-STOP!");
        air_detect = 1;

        // --- SKENARIO 5: Reset Sistem ---
        #30;
        $display("\n[TEST] Masalah selesai, Reset ditekan");
        air_detect = 0; 
        reset = 1;      
        #10 reset = 0;

        #20;
        $display("\n--- SIMULASI SELESAI ---");
        $finish; 
    end

endmodule