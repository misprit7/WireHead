using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Terraria;
using Terraria.DataStructures;

namespace WireHead;

internal static class TerraCC
{
    // Do I use inconsistent method/variable naming conventions? Yes. 
    // I realy don't like caps in my names though so whatever

    /**********************************************************************
     * Constants
     *********************************************************************/

    private const int max_depth = 1000;
    private const string work_dir = "/tmp/terracc/";
    private const string c_file_name = "wld.c";
    private const string so_file_name = "wld.so";

    /**********************************************************************
     * Variables
     *********************************************************************/

    private static List<int> to_fn = new List<int>();

    /**********************************************************************
     * Private Functions
     *********************************************************************/

    private static string group_fn_name_str(int g){
        return $"trigger{g}";
    }

    private static string group_fn_str(int g){
        to_fn.Add(g);
        return group_fn_name_str(g);
    }

    private static string group_exec_str(int[] groups){
        if(groups.Length == 1){
            return $"{group_fn_str(groups[0])}();\n";
        }

        string ret = "";

        foreach(int g in groups){
            if(Accelerator.toggleable[g].Length > 0)
                ret += $"tog(s[{g}]);\n";
        }
        ret += "\n";

        // TODO non-standard triggerables
        /* List<uint> to_trigger = new List<uint>(); */
        List<uint> std_lamps = new List<uint>();
        foreach(int g in groups){
            std_lamps.AddRange(Accelerator.groupStandardLamps[g]);
        }

        return ret;
    }

    private static string switch_str(){
        string ret = "";
        ret += "switch(trig[i]){\n";
        for(int i = 1; i < Accelerator.numGroups; ++i){
            if(Accelerator.groupStandardLamps[i].Count == 0) continue;
            ret += $"case {i}:\n";
            foreach(uint std_lamp in Accelerator.groupStandardLamps[i]){
                List<string> xor = new List<string>();
                Point16 p = Accelerator.uint2Point(std_lamp);
                for(int c = 0; c < Accelerator.colors; ++c){
                    int g = Accelerator.wireGroup[p.X, p.Y, c];
                    if(g > 0) xor.Add($"s[{g}]");
                }
                bool on = Main.tile[p.X, p.Y+1].TileFrameX == 18;
                if(xor.Count > 0){
                    ret += "if(";
                    if(on)
                        ret += string.Join(" ^ ", xor);
                    else
                        ret += "!(" + string.Join(" ^ ", xor) + ")";
                    ret += "){\n";
                }
                if(xor.Count > 0 || on){
                    bool bottom_empty = true;
                    for(int c = 0; c < Accelerator.colors; ++c){
                        int g = Accelerator.wireGroup[p.X, p.Y, c];
                        if(g > 0){
                            bottom_empty = false;
                            ret += $"trig_next[num_trig_next++] = {g};\n";
                        }
                    }
                    if(bottom_empty) ret += "(void)\n";
                }
                if(xor.Count > 0){
                    ret += "break;\n}\n";
                }

            }
        }
        ret += @"
default:
#ifdef unreachable
unreachable();
#else
{};
#endif
";
        ret += "}\n";
        return ret;
    }


    /**********************************************************************
     * Public Functions
     *********************************************************************/

    public static void transpile()
    {

         string c_file = $@"
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <stddef.h>

#define num_groups {Accelerator.numGroups}
#define colors 4
#define max_triggers 1000

static bool s[num_groups] = {{{string.Join(", ", Accelerator.groupState.Take(Accelerator.numGroups).Select(b => b ? "1" : "0"))}}};

void trigger(int input_groups[][colors], uint32_t num_inputs){{
    printf(""input: %d\n"", input_groups[0][0]);
    for(int j = 0; j < num_inputs; ++j){{

        int num_trig = 0;
        int num_trig_next = 0;
        int to_trigger1[max_triggers];
        int to_trigger2[max_triggers];

        // Switch back between these to avoid memcpy
        int *trig = to_trigger1;
        int *trig_next = to_trigger2;

        // Load initial trigger groups
        for(num_trig = 0; num_trig < colors; ++num_trig){{
            if(input_groups[j][num_trig] == -1) break;
            trig[num_trig] = input_groups[j][num_trig];
        }}
        
        while(num_trig > 0){{
            for(int i = 0; i < num_trig; ++i){{
                s[trig[i]] = !s[trig[i]];
            }}

            num_trig_next = 0;

            for(int i = 0; i < num_trig; ++i){{
                {switch_str()}
            }}

            // Switch buffer assignment
            int *tmp = trig_next;
            trig_next = trig;
            trig = tmp;
        }}
    }}
}}

void read_states(bool *states){{
    printf(""Reading from c!\n"");
    memcpy(states, s, num_groups * sizeof(s[0]));
}}

/*int main(void){{
    //int triggers[][4] = {{{{-1, -1, -1, 1}}}};
    bool states[num_groups] = {{0}};
    read_states(states);
    for(int i = 0; i < num_groups; ++i){{
        printf(""%d "", states[i]);
    }}
    printf(""\\n"");
}}*/

";
        try
        {
            if (!Directory.Exists(work_dir))
            {
                Directory.CreateDirectory(work_dir);
                Console.WriteLine("Directory created successfully.");
            }
            // Write the string content to the file
            File.WriteAllText(work_dir + c_file_name, c_file);

            Console.WriteLine("File written successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    
    }

    public static void compile(){
        // Create a ProcessStartInfo object
        ProcessStartInfo processInfo = new ProcessStartInfo("gcc", $"-fpic -shared -O3 -o {work_dir}{so_file_name} {work_dir}{c_file_name}");
        processInfo.RedirectStandardOutput = true;
        processInfo.UseShellExecute = false;
        processInfo.CreateNoWindow = true;

        // Create a new Process instance
        Process process = new Process();
        process.StartInfo = processInfo;

        // Start the process
        process.Start();

        // Read the standard output of the command
        string output = process.StandardOutput.ReadToEnd();

        // Wait for the process to finish
        process.WaitForExit();

        enable();
    }

    private static IntPtr libHandle;
    private const int RTLD_LAZY = 1;
    private const string DlLibrary = "/usr/lib/libdl.so.2";

    public static void enable(){
        libHandle = dlopen(work_dir + so_file_name, RTLD_LAZY);
        if (libHandle == IntPtr.Zero)
        {
            Console.WriteLine("Error Loading libdl");
            // Handle error if library couldn't be loaded
            IntPtr error = dlerror();
            string errorMessage = Marshal.PtrToStringAnsi(error);
            Console.WriteLine($"Error loading library: {errorMessage}");
            return;
        }

        IntPtr trigger_ptr = dlsym(libHandle, "trigger");
        IntPtr read_states_ptr = dlsym(libHandle, "read_states");

        TriggerDelegate trigger = Marshal.GetDelegateForFunctionPointer<TriggerDelegate>(trigger_ptr);
        ReadStatesDelegate read_states = Marshal.GetDelegateForFunctionPointer<ReadStatesDelegate>(read_states_ptr);

        /* Console.WriteLine("Sucessful call"); */
    }

    public static void disable(){
        dlclose(libHandle);
        
    }
    public static TriggerDelegate trigger;
    public static ReadStatesDelegate read_states;

    /**********************************************************************
     * Imported functions
     *********************************************************************/

    [DllImport(DlLibrary, SetLastError = true)]
    private static extern IntPtr dlopen(string filename, int flags);

    [DllImport(DlLibrary)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport(DlLibrary)]
    private static extern IntPtr dlerror();

    [DllImport(DlLibrary)]
    private static extern int dlclose(IntPtr handle);

    public delegate void TriggerDelegate(int[,] input_groups, uint num_inputs);
    public delegate void ReadStatesDelegate([Out] bool[] states);

}
