namespace GeneralImprovements.Utilities
{
    internal static class WeatherASCIIArt
    {
        public static string[] ClearAnimations = new string[]
        {
@"\   ___   /
  -|    |-
/   ---   \
  CLEAR!",
@"\   ___   /
-  |    | -
/   ---   \
  CLEAR!",
        };
        public static string[] RainAnimations = new string[]
        {
@"/~~~~~~\
\~~~~~~/
    | | | |
  | | | |",

@"/~~~~~~\
\~~~~~~/
  | | | |
    | | | |",
        };
        public static string[] FoggyAnimations = new string[]
        {
@"~~~~~~~~
~~~~~~~~
~~~~~~~~
~~~~~~~~"
        };
        public static string[] FloodedAnimations = new string[]
        {
@"/~~~~~~\
\~~~~~~/
    | | | |
~~~~~~~~",

@"/~~~~~~\
\~~~~~~/
~~~~~~~~
~~~~~~~~",
        };
        public static string[] EclipsedAnimations = new string[]
        {
@"   -----
  |         |
  |         |
   -----",
@"   -----
  |         |
  |         |
   -----",
@"   -----
  |         |
  |         |
   -----",
@"   -----
\|         |
/|         |
   -----",
@"   -----
-\        |
-/        |
   -----",
@"   -----
/-\      |
\-/      |
   -----",
@"   -----
  /-\    |
  \-/    |
   -----",
@"   -----
  | /-\ |
  | \-/ |
   -----",
@"   -----
  | /-\ |
  | \-/ |
   -----",
@"   -----
  | /-\ |
  | \-/ |
   -----",
@"   -----
  |    /-\
  |    \-/
   -----",
@"   -----
  |      /-\
  |      \-/
   -----",
@"   -----
  |        /-
  |        \-
   -----",
@"   -----
  |         /-
  |         \-
   -----",
@"   -----
  |         |/
  |         |\
   -----"
        };
        public static string[] UnknownAnimations = new string[]
        {
@"  ?       ?
?     ?
    ?     ?
?  ?        ?",
@"?   ?       ?
   ?    ?
?      ?   
    ?   ?    ?",
@"      ?
?   ?      ?
  ?     ?
 ?         ?"
        };
        public static string[] LightningOverlays = new string[]
        {
@"  \
   \
     /
   *",
@"     /
    /
       \
        *",
        };
    }
}
