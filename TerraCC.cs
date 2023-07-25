using System;
using System.Text;
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
    
    /**
     * (Incomplete) Assumptions used for this module:
     * 1. Logic gates never smoke
     * 2. The same group is never triggered twice in the same wire eval
     * 3. Pixel boxes are arranged nicely so that >= 2 group triggers=toggle
     */

    /**********************************************************************
     * Constants
     *********************************************************************/

    // File paths for temporary working directory to compile stuff in
    private const string work_dir = "/tmp/terracc/";
    private const string c_file_name = "wld.c";
    private const string so_file_name = "wld.so";

    // libdl parameters
    private const int RTLD_LAZY = 1;
    private const string DlLibrary = "/usr/lib/libdl.so.2";


    /**********************************************************************
     * Variables
     *********************************************************************/

    // Handle for libdl functions
    private static IntPtr libHandle;

    /**********************************************************************
     * Private Functions
     *********************************************************************/

    /**
     * String representing triggering a pixel box trigger
     */
    private static string pb_str(){
        string ret = "";

        ret += "switch(trig[i]){\n";

        for(int g1 = 0; g1 < Accelerator.numGroups; ++g1){
            var pb_dict = Accelerator.pixelBoxes[g1];
            if(pb_dict.Count == 0) continue;

            ret += $"case {g1}:\n";
            ret += "for(int j = i+1; j < num_trig; ++j){\n";
            ret += "switch(trig[j]){\n";

            foreach(var entry in pb_dict){
                int g2 = entry.Key;
                ret += $"case {g2}:\n";
                /* ret += "printf(\"Toggling pixel box, state %d\\n\", pb_s[0]);\n"; */
                ret += $"tog(pb_s[{Accelerator.pbCoord2Id[entry.Value]}]);\n";
                ret += "break;\n";
            }

            ret += "}\n}\nbreak;\n";
        }
        
        ret += "}\n";

        return ret;
    }

    /*
     * String representing what to do for each possible group
     */
    private static string faulty_str(){
        StringBuilder ret = new StringBuilder();
        ret.Append("switch(trig[i]){\n");
        for(int i = 1; i < Accelerator.numGroups; ++i){
            if(Accelerator.groupStandardLamps[i].Count == 0) continue;
            ret.Append($"case {i}:\n");
            foreach(uint std_lamp in Accelerator.groupStandardLamps[i]){
                List<string> xor = new List<string>();
                Point16 p = Accelerator.uint2Point(std_lamp);
                for(int c = 0; c < Accelerator.colors; ++c){
                    int g = Accelerator.wireGroup[p.X, p.Y+1, c];
                    if(g > 0) xor.Add($"s[{g}]");
                }
                bool on = Main.tile[p.X, p.Y+1].TileFrameX == 18;
                if(xor.Count > 0){
                    ret.Append("if(");
                    if(!on)
                        ret.Append(string.Join(" ^ ", xor));
                    else
                        ret.Append("!(" + string.Join(" ^ ", xor) + ")");
                    ret.Append("){\n");
                }
                if(xor.Count > 0 || on){
                    for(int c = 0; c < Accelerator.colors; ++c){
                        int g = Accelerator.wireGroup[p.X, p.Y+2, c];
                        if(g > 0){
                            ret.Append($"trig_next[num_trig_next++] = {g};\n");
                        }
                    }
                }
                if(xor.Count > 0){
                    ret.Append("}\n");
                }
            }
            ret.Append("break;\n");
        }
        ret.Append(@"
case -1:
case 0:
break;
default:
#ifdef unreachable
unreachable();
#else
{};
#endif
");
        ret.Append("}\n");
        return ret.ToString();
    }


    /**********************************************************************
     * Public Functions
     *********************************************************************/

    /*
     * Transpile a Terraria wiring world into a c program with the same control
     * graph
     * I wish there was an equivalent of #include in C#, but since there isn't
     * I'm sticking to string blocks
     */
    public static void transpile()
    {

         string c_file = $@"
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <stddef.h>

#define tog(x) (x=!x)

#define num_groups {Accelerator.numGroups+1}
#define num_pb {Accelerator.numPb}
#define colors 4
#define max_triggers {Accelerator.maxTriggers}
#define max_depth 5000

// Wire states
static bool s[num_groups] = {{{string.Join(", ", Accelerator.groupState.Take(Accelerator.numGroups+1).Select(b => b ? "1" : "0"))}}};

// Pixel boxes
static bool pb_s[num_pb] = {{0}};

void trigger(int input_groups[][colors], int32_t num_inputs){{
    /* printf(""input: %d\n"", input_groups[0][0]); */
    for(int j = 0; j < num_inputs; ++j){{

        int num_trig = 0;
        int num_trig_next = 0;
        int to_trigger1[max_triggers];
        int to_trigger2[max_triggers];

        // Switch back between these to avoid memcpy
        int *trig = to_trigger1;
        int *trig_next = to_trigger2;

        // Load initial trigger groups
        for(int c = 0; c < colors; ++c){{
            if(input_groups[j][c] <= 0) continue;
            trig[num_trig] = input_groups[j][c];
            ++num_trig;
        }}
        
        int iter = 0;
        while(num_trig > 0){{
            if(iter >= max_depth){{
                printf(""Max depth exceeded!\n"");
                break;
            }} else ++iter;

            for(int i = 0; i < num_trig; ++i){{
                tog(s[trig[i]]);
                {pb_str()}
            }}

            for(int i = 0; i < num_trig; ++i){{
                {faulty_str()}
            }}

            // Switch buffer assignment
            int *tmp = trig_next;
            trig_next = trig;
            trig = tmp;

            num_trig = num_trig_next;
            num_trig_next = 0;
        }}
    }}
}}

void read_states(uint8_t *states){{
    memcpy(states, s, num_groups * sizeof(s[0]));
}}

void read_pb(uint8_t *pb_states){{
    memcpy(pb_states, pb_s, num_pb * sizeof(pb_s[0]));
    memset(pb_s, 0, num_pb * sizeof(pb_s[0]));
}}

int main(void){{
    int triggers[1][4] = {{{{15, 16, -1, -1}}}};
    trigger(triggers, 1);

    uint8_t states[num_groups] = {{0}};
    read_states(states);
    uint8_t pb_states[num_groups] = {{0}};
    read_pb(pb_states);

    for(int i = 0; i < num_groups; ++i){{
        printf(""%d "", states[i]);
    }}
    printf(""\n"");
    for(int i = 0; i < num_pb; ++i){{
        printf(""%d "", pb_states[i]);
    }}
    printf(""\n"");
}}

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
        ProcessStartInfo processInfo = new ProcessStartInfo(
            "gcc",
            $"-fpic -shared -O3 -o {work_dir}{so_file_name} {work_dir}{c_file_name}"
        );
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.UseShellExecute = false;
        processInfo.CreateNoWindow = true;

        // Create a new Process instance
        Process process = new Process();
        process.StartInfo = processInfo;

        // Start the process
        process.Start();

        // Read the standard output of the command
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        // Wait for the process to finish
        process.WaitForExit();
        if (process.ExitCode != 0)
            {
                // Handle the error here or throw an exception if needed
                Console.WriteLine($"Error occurred. Exit code: {process.ExitCode}");
                Console.WriteLine($"Error message: {error}");
                throw new InvalidOperationException("Compiling Failed!");
            }
    }

    public static void enable(){
        if(libHandle != IntPtr.Zero){
            disable();
        }

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
        IntPtr read_pb_ptr = dlsym(libHandle, "read_pb");

        trigger = Marshal.GetDelegateForFunctionPointer<TriggerDelegate>(trigger_ptr);
        read_states = Marshal.GetDelegateForFunctionPointer<ReadStatesDelegate>(read_states_ptr);
        read_pb = Marshal.GetDelegateForFunctionPointer<ReadPbDelegate>(read_pb_ptr);

        WireHead.useTerracc = true;
        Console.WriteLine("terracc enabled");
    }

    public static void disable(){
        if(WireHead.useTerracc){
            WireHead.useTerracc = false;
            dlclose(libHandle);
            libHandle = IntPtr.Zero;
            Console.WriteLine("terracc disabled");
        }
        
    }

    public static TriggerDelegate trigger;
    public static ReadStatesDelegate read_states;
    public static ReadPbDelegate read_pb;

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

    public delegate void TriggerDelegate(int[,] input_groups, int num_inputs);
    public delegate void ReadStatesDelegate([Out] byte[] states);
    public delegate void ReadPbDelegate([Out] byte[] pb_states);

}
