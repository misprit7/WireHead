using System;

namespace WireHead;

internal static class TerraCC
{
    public static void Transpile()
    {

         string c_file = $@"
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

#define num_groups {Accelerator.numGroups}
#define colors 4
#define max_triggers 1000

static bool group_state[num_groups] = {{{string.Join(", ", Array.ConvertAll(Accelerator.groupState, b => b ? "1" : "0"))}}};

void trigger(int input_groups[], uint32_t num_inputs){{
    int num_to_trigger = num_inputs;
    int to_trigger[max_triggers];
    memcpy(to_trigger, input_groups, num_inputs * sizeof(to_trigger[0]));
    
    while(num_to_trigger > 0){{
        for(int i = 0; i < num_to_trigger; ++i){{
            group_state[to_trigger[i]] = !group_state[to_trigger[i]];
        }}

        int to_trigger_old[num_to_trigger];
        memcpy(to_trigger_old, to_trigger, num_to_trigger * sizeof(to_trigger[0]));
        int num_to_trigger_old = num_to_trigger;
        num_to_trigger = 0;

        for(int i = 0; i < num_to_trigger_old; ++i){{
            switch(to_trigger_old[i]){{
                case 1:
                    if(!(group_state[2]^group_state[3])){{
                        to_trigger[num_to_trigger++] = 3;
                    }}

                    break;
            }}
        }}
    }}
}}

";
    }

}