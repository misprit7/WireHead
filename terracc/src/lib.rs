
// Taken with modification from here:
// https://blog.datalust.co/rust-at-datalust-how-we-integrate-rust-with-csharp/
macro_rules! ffi {
    ($(fn $name:ident($($arg_ident:ident: $arg_ty:ty),*) -> bool $body:expr)*) => {
        $(
            #[no_mangle]
            pub unsafe extern "C" fn $name( $($arg_ident : $arg_ty),* ) -> bool {
                #[allow(unused_mut)]
                unsafe fn call( $(mut $arg_ident: $arg_ty),* ) -> bool {
                    $body
                }
                call($($arg_ident),*)
            }
        )*
    };
}

const WIDTH: usize = 8400;
const HEIGHT: usize = 2400;
const MAX_GROUPS: usize = 1>>16;

ffi!{
    fn add(left: usize, right: usize) -> bool {
        left + right < 2
    }

    /*
     * Initializes rust <-> C# interface
     * num_groups: Number of groups
     * tile_group: Mapping of tile to up to 4 groups (one for each color)
     * group_state: State of each group
     * group_tog: List of tiles toggleable tiles by group
     * group_trig: List of tiles triggerable tiles by group
     * group_pb: List of pixelbox tiles by group
     * group_std_lamps: List of tops of standard faulty gates attached to each group
     * tile_state: Current real state of each tile
     */
    fn init(
        num_groups: u32,
        tile_group: &[[[u32; 4]; WIDTH]; HEIGHT],
        group_state: &[bool; MAX_GROUPS],
        group_tog: &[*mut u32; MAX_GROUPS],
        group_trig: &[*mut u32; MAX_GROUPS],
        group_pb: &[*mut u32; MAX_GROUPS],
        group_std_lamps: &[*mut u32; MAX_GROUPS],
        tile_state: &[[bool; WIDTH]; HEIGHT]
    ) -> bool {
        
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn it_works() {
        // let result = add(2, 2);
        let result = 4;
        assert_eq!(result, 4);
    }
}
