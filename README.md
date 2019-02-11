# NativePinvokeFastCountSubDirsandFiles
Here's a fast C# native method using P/Invoke to count all files and sub-directories. It uses FindNextFile  filing information in WIN32_FIND_DATA structure. Using the implementation below it does offer the ability to do extensive error handling on a file-by-file basis. It compares this to GetFile and GetDirectories methods, with elapsed times.
