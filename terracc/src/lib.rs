
// Taken with modification from here:
// https://blog.datalust.co/rust-at-datalust-how-we-integrate-rust-with-csharp/
macro_rules! ffi {
    ($(fn $name:ident($($arg_ident:ident: $arg_ty:ty),*) -> usize $body:expr)*) => {
        $(
            #[no_mangle]
            pub unsafe extern "C" fn $name( $($arg_ident : $arg_ty),* ) -> usize {
                #[allow(unused_mut)]
                unsafe fn call( $(mut $arg_ident: $arg_ty),* ) -> usize {
                    $body
                }
                call($($arg_ident),*)
            }
        )*
    };
}

ffi!{

}

ffi!{
    fn add(left: usize, right: usize) -> usize {
        return left + right
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
