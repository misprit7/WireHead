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
     * Convenience method to write to file
     */
    private static void write_file(string file_name, string file){
        try
        {
            if (!Directory.Exists(work_dir))
            {
                Directory.CreateDirectory(work_dir);
                Console.WriteLine("Directory created successfully.");
            }
            // Write the string content to the file
            File.WriteAllText(work_dir + file_name, file);

            Console.WriteLine("File written successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    /**
     * String representing triggering a pixel box trigger
     * Also writes helper files to interface with
     */
    private static string pb_str(){
        string ret = "";

        // pb_pairs: group1, group2, pbid

        // If I had easy access to a hash set this should really be O(1) set inclusion but whatever
        ret += $@"
int j = 0;
int g = trig[i];
while(pb_pairs[g][j][0] != -1){{
    printf(""Anotherpb\n"");
    for(int k = i+1; k < num_trig; ++k){{
        if(trig[k] == pb_pairs[g][j][0]){{
            tog(pb_s[pb_pairs[g][j][1]]);
        }}
    }}
    ++j;
}}
";

        // pb_pairs data
        StringBuilder pb_pairs_data = new StringBuilder();
        for(int g1 = 0; g1 < Accelerator.numGroups; ++g1){
            pb_pairs_data.Append("{\n");
            foreach(var (g2, pbid) in Accelerator.pixelBoxes[g1]){
                pb_pairs_data.Append($"{{{g2},{pbid}}},\n");
            }
            pb_pairs_data.Append("{-1,-1},\n");
            pb_pairs_data.Append("},\n");
        }


        write_file("pb_pairs_data.txt", pb_pairs_data.ToString());

        return ret;


        /* ret += "switch(trig[i]){\n"; */

        /* for(int g1 = 0; g1 < Accelerator.numGroups; ++g1){ */
        /*     var pb_dict = Accelerator.pixelBoxes[g1]; */
        /*     if(pb_dict.Count == 0) continue; */

        /*     ret += $"case {g1}:\n"; */
        /*     ret += "for(int j = i+1; j < num_trig; ++j){\n"; */
        /*     ret += "switch(trig[j]){\n"; */

        /*     foreach(var entry in pb_dict){ */
        /*         int g2 = entry.Key; */
        /*         ret += $"case {g2}:\n"; */
        /*         /1* ret += "printf(\"Toggling pixel box, state %d\\n\", pb_s[0]);\n"; *1/ */
        /*         ret += $"tog(pb_s[{entry.Value}]);\n"; */
        /*         ret += "break;\n"; */
        /*     } */

        /*     ret += "}\n}\nbreak;\n"; */
        /* } */
        
        /* ret += "}\n"; */

        return ret;
    }

    /*
     * String representing what to do for each possible group
     * Also writes helper files to interface with since I hate myself and love side effects
     */
    private static string faulty_str(){
        StringBuilder ret = new StringBuilder();
        // Extra standard lamps not in the switch statement
        List<int[,]>[] extra_std = new List<int[,]>[Accelerator.numGroups];

        ret.Append("switch(trig[i]){\n");
        for(int i = 0; i < Accelerator.numGroups; ++i){
            extra_std[i] = new List<int[,]>();
            if(Accelerator.groupStandardLamps[i].Count == 0) continue;
            ret.Append($"case {i}:\n");
            foreach(uint std_lamp in Accelerator.groupStandardLamps[i]){
                // Update extra List
                // First is current wire state, second is middle lamp groups, last
                // is output groups
                int[,] lamp_data = new int[3,Accelerator.colors]{
                        {
                            1, // Entry populated
                            0, // Middle gate on
                            0, // Unused
                            0, // Unused
                        },
                        {-1, -1, -1, -1},
                        {-1, -1, -1, -1}
                };

                List<string> xor = new List<string>();
                Point16 p = Accelerator.uint2Point(std_lamp);
                for(int c = 0, j = 0; c < Accelerator.colors; ++c){
                    int g = Accelerator.wireGroup[p.X, p.Y+1, c];
                    if(g >= 0){
                        xor.Add($"s[{g}]");
                        lamp_data[1, j++] = g;
                    }
                }
                bool on = Main.tile[p.X, p.Y+1].TileFrameX == 18;
                if(on) lamp_data[0, 1] = 1;
                if(xor.Count > 0){
                    ret.Append("if(");
                    if(!on)
                        ret.Append(string.Join(" ^ ", xor));
                    else
                        ret.Append("!(" + string.Join(" ^ ", xor) + ")");
                    ret.Append("){\n");
                }
                if(xor.Count > 0 || on){
                    for(int c = 0, j = 0; c < Accelerator.colors; ++c){
                        int g = Accelerator.wireGroup[p.X, p.Y+2, c];
                        if(g >= 0){
                            ret.Append($"trig_next[num_trig_next++] = {g};\n");
                            lamp_data[2, j++] = g;
                        }
                    }
                }
                extra_std[i].Add(lamp_data);
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

        // Extra standard lamps handling
        StringBuilder std_lamps = new StringBuilder();
        int max_connections = extra_std.Max(list => list.Count());
        std_lamps.Append($"#define max_connections {max_connections}\n");
        std_lamps.Append(
            $"static int std_lamps[{Accelerator.numGroups}][{max_connections}][3][{Accelerator.colors}];"
        );
        write_file("std_lamps.h", std_lamps.ToString());

        StringBuilder std_lamps_data = new StringBuilder();
        for(int g = 0; g < Accelerator.numGroups; ++g){
            std_lamps_data.Append("{\n");
            foreach(var std_lamp in extra_std[g]){
                std_lamps_data.Append("{");
                for(int i = 0; i < 3; ++i){
                    std_lamps_data.Append($"{{{std_lamp[i,0]},{std_lamp[i,1]},{std_lamp[i,2]},{std_lamp[i,3]}}},");
                }
                std_lamps_data.Append("},\n");
            }
            std_lamps_data.Append("},\n");
        }


        write_file("std_lamps_data.txt", std_lamps_data.ToString());

        return $@"
for(int j = 0; j < max_connections; ++j){{
    if(std_lamps[trig[i]][j][0][0] == 0) break;
    bool xor = std_lamps[trig[i]][j][0][1];
    for(int c = 0; c < {Accelerator.colors}; ++c){{
        int g = std_lamps[trig[i]][j][1][c];
        if(g < 0) break;
        xor ^= s[g];
    }}
    if(xor){{
        for(int c = 0; c < {Accelerator.colors}; ++c){{
            int g = std_lamps[trig[i]][j][2][c];
            if(g < 0) break;
            trig_next[num_trig_next++] = g;
        }}
    }}
}}
        ";

        /* return ret.ToString(); */
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
        Accelerator.Preprocess();
        string c_file = $@"
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
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
// This is really space innefficient, could malloc for this instead
static int pb_pairs[num_groups][200][2];

// Clock monitor
static int clock_group = {Accelerator.clockGroup};
static int clock_count = 0;

// Standard lamp connections
#include ""std_lamps.h""

void init(){{
    FILE *std_lamps_f = fopen(""/tmp/terracc/std_lamps_data.txt"", ""r"");
    if(std_lamps_f == NULL){{
        perror(""Failed to open std_lamps_data.txt!"");
        return;
    }}
    for(int i = 0; i < sizeof(std_lamps)/sizeof(std_lamps[0]); ++i){{
        char line[100];

        // Gets first {{,
        fgets(line, sizeof(line), std_lamps_f);
        if(line[0] != '{{'){{
            printf(""Read format not as expected!"");
        }}
        // Gets data
        fgets(line, sizeof(line), std_lamps_f);

        int j = 0;
        while(sscanf(line, ""{{{{%d,%d,%d,%d}},{{%d,%d,%d,%d}},{{%d,%d,%d,%d}},}},"",
                    &std_lamps[i][j][0][0], &std_lamps[i][j][0][1], &std_lamps[i][j][0][2], &std_lamps[i][j][0][3],
                    &std_lamps[i][j][1][0], &std_lamps[i][j][1][1], &std_lamps[i][j][1][2], &std_lamps[i][j][1][3],
                    &std_lamps[i][j][2][0], &std_lamps[i][j][2][1], &std_lamps[i][j][2][2], &std_lamps[i][j][2][3]
                    ) == 12){{
            fgets(line, sizeof(line), std_lamps_f);
            ++j;
        }}
        // Data has been read so line should be }},
        if(line[0] != '}}'){{
            printf(""Read format not as expected!"");
        }}
    }}
    fclose(std_lamps_f);

    FILE *pb_pairs_f = fopen(""/tmp/terracc/pb_pairs_data.txt"", ""r"");
    if(pb_pairs_f == NULL){{
        perror(""Failed to open pb_pairs_data.txt!"");
        return;
    }}
    for(int i = 0; i < sizeof(pb_pairs)/sizeof(pb_pairs[0]); ++i){{
        char line[10000];

        // Gets first {{,
        fgets(line, sizeof(line), pb_pairs_f);
        if(line[0] != '{{'){{
            printf(""Read format not as expected!"");
        }}
        // Gets data
        fgets(line, sizeof(line), pb_pairs_f);

        int j = 0;
        while(sscanf(line, ""{{%d,%d}},"",
                    &pb_pairs[i][j][0], &pb_pairs[i][j][1]) == 2){{
            fgets(line, sizeof(line), pb_pairs_f);
            ++j;
        }}
        // Data has been read so line should be }},
        if(line[0] != '}}'){{
            printf(""Read format not as expected!"");
        }}
    }}
    fclose(pb_pairs_f);

    /* FILE *file; */
    /* char *filename = ""/tmp/test.txt""; */

    /* // Open the file for writing */
    /* file = fopen(filename, ""w""); */
    /* // Write data to the file */
    /* fprintf(file, ""Finished init\n""); */

    /* // Close the file */
    /* fclose(file); */

    /* /1* printf(""Finished init!\n""); *1/ */
}}

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
            if(input_groups[j][c] < 0) continue;
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
                if(trig[i] == clock_group) ++clock_count;
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

int read_clock(){{
    return clock_count;
}}

void set_clock(int group){{
    clock_group = group;
    clock_count = 0;
}}

int main(void){{

    init();

    int triggers[1][4] = {{{{4, -1, -1, -1}}}};
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

    for(int i = 0; i < sizeof(std_lamps) / sizeof(std_lamps[0]); ++i){{
        for(int j = 0; j < 12; ++j){{
            printf(""%d, "", std_lamps[i][0][0][j]);
        }}
        printf(""\n"");
    }}

    for(int i = 0; i < sizeof(pb_pairs) / sizeof(pb_pairs[0]); ++i){{
        int j = 0;
        while(pb_pairs[i][j][0] != -1){{
            printf(""%d, %d\n"", pb_pairs[i][j][0], pb_pairs[i][j][1]);
            ++j;
        }}
        printf(""\n"");
    }}
}}

";
        write_file(c_file_name, c_file);
    
    }

    public static void compile(){
        // Create a ProcessStartInfo object
        ProcessStartInfo processInfo = new ProcessStartInfo(
            "gcc",
            $"-fpic -mcmodel=large -shared -O1 -o {work_dir}{so_file_name} {work_dir}{c_file_name}"
        );
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.UseShellExecute = false;
        processInfo.CreateNoWindow = true;

        // Create a new Process instance
        Process process = new Process();
        process.StartInfo = processInfo;

        // Start the process
        Console.WriteLine($"Started compiling");
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
            throw new InvalidOperationException($"Compiling failed with exit code {process.ExitCode} : {error}");
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
        IntPtr read_clock_ptr = dlsym(libHandle, "read_clock");
        IntPtr set_clock_ptr = dlsym(libHandle, "set_clock");
        IntPtr init_ptr = dlsym(libHandle, "init");

        trigger = Marshal.GetDelegateForFunctionPointer<TriggerDelegate>(trigger_ptr);
        read_states = Marshal.GetDelegateForFunctionPointer<ReadStatesDelegate>(read_states_ptr);
        read_pb = Marshal.GetDelegateForFunctionPointer<ReadPbDelegate>(read_pb_ptr);
        read_clock = Marshal.GetDelegateForFunctionPointer<ReadClockDelegate>(read_clock_ptr);
        set_clock = Marshal.GetDelegateForFunctionPointer<SetClockDelegate>(set_clock_ptr);
        init = Marshal.GetDelegateForFunctionPointer<InitDelegate>(init_ptr);

        WireHead.useTerracc = true;
        Console.WriteLine("Reading in external data");
        init();
        if(Accelerator.clockGroup != -1){
            set_clock(Accelerator.clockGroup);
        }
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
    public static ReadClockDelegate read_clock;
    public static SetClockDelegate set_clock;
    public static InitDelegate init;

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
    public delegate int ReadClockDelegate();
    public delegate void SetClockDelegate(int group);
    public delegate void InitDelegate();

}
