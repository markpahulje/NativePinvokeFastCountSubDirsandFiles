using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Linq; 

namespace NativePinvokeFastCountSubDirsandFiles
{
    static class Program
    {
        public static long filecnt = 0;
        public static long dircnt = 0;
        
        //https://social.msdn.microsoft.com/Forums/en-US/944884bc-aa37-4437-b148-1ab169d9a893/counting-folders-on-a-drive?forum=csharplanguage
        //seems like a copy from https://www.codeproject.com/Articles/12782/File-System-Enumerator-using-lazy-matching
        static void Main()
        {
            int count = 0;

            string startdir = @"C:\build\";
 
            Stopwatch sw = new Stopwatch();
            sw.Start();

            foreach (var item in RecursiveFolderContents(startdir, null, FolderContentType.FilesAndFolders))
            {
                ++count;
            }
            sw.Stop();
            Console.WriteLine("Fast Pinvoke Enumeration of directory " + startdir);
            Console.WriteLine(" Dirs Count = " + dircnt.ToString("N0"));
            Console.WriteLine("Files Count = " + filecnt.ToString("N0"));
            Console.Write ("Total number files and sub-directories = "); 
            Console.WriteLine(count.ToString("N0") + " in " + sw.ElapsedMilliseconds.ToString("N0") + " ms."); //for blog

            sw.Restart();

            dircnt = System.IO.Directory.GetDirectories(startdir, "*", SearchOption.AllDirectories).Count();
            filecnt = System.IO.Directory.GetFiles(startdir, "*", SearchOption.AllDirectories).Count();

            sw.Stop();

            Console.WriteLine(); 
            Console.WriteLine("GetFiles & GetDirectories Enumeration of directory " + startdir);
            Console.WriteLine(" Dirs Count = " + dircnt.ToString("N0"));
            Console.WriteLine("Files Count = " + filecnt.ToString("N0"));
            Console.Write("Total number files and sub-directories = ");

            Console.WriteLine((filecnt + dircnt).ToString("N0") + " in " + sw.ElapsedMilliseconds.ToString("N0") + " ms."); //for blog



            if (Debugger.IsAttached)
                Console.ReadKey(); 


        }

     
        /// <summary>Used with to specify the type of items to be returned.</summary>
        public enum FolderContentType
        {
            /// <summary>Files and folders returned.</summary>

            FilesAndFolders,

            /// <summary>Only folders returned.</summary>

            FoldersOnly,

            /// <summary>Only files returned.</summary>

            FilesOnly
        }

        /// <summary>
        /// Recursively lists the items in a folder that have a certain suffix (wildcards are not supported).
        /// without having to return them all at once via an array.
        /// This is useful if there are a very large number of files in a folder.
        /// </summary>
        /// <param name="folderName">The name of a folder.</param>
        /// <param name="fileSuffix">
        /// A file suffix to match, or "" or null to match all files.
        /// This is used only for files; if folders are being returned, all folders are returned regardless of their suffixes.
        /// </param>
        /// <param name="contentType">Type of the content to be returned.</param>
        /// <returns>An enumerator for all the items.</returns>

        public static IEnumerable<FolderItem> RecursiveFolderContents
        (
            string folderName,
            string fileSuffix,
            FolderContentType contentType
        )
        {
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!string.IsNullOrEmpty(fileSuffix))
            {
                if (fileSuffix[0] != '.')
                {
                    fileSuffix = "." + fileSuffix;
                }
            }

            return recursiveFolderContents(folderName, fileSuffix, contentType);
        }

        /// <summary>
        /// Recursively lists the items in a folder that have a certain suffix (wildcards are not supported).
        /// without having to return them all at once via an array.
        /// This is useful if there are a very large number of files in a folder.
        /// </summary>
        /// <param name="folderName">The name of a folder.</param>
        /// <param name="fileSuffix">
        /// A file suffix to match, or "" or null to match all files.
        /// This is used only for files; if folders are being returned, all folders are returned regardless of their suffixes.
        /// </param>
        /// <param name="contentType">Type of the content to be returned.</param>
        /// <returns>An enumerator for all the items.</returns>

        private static IEnumerable<FolderItem> recursiveFolderContents
        (
            string folderName,
            string fileSuffix,
            FolderContentType contentType
        )
        {
            bool wantAllFiles = string.IsNullOrEmpty(fileSuffix);

            foreach (FolderItem item in FolderContents(folderName, null, FolderContentType.FilesAndFolders))
            {
                if (item.IsFolder)  // Visit all items in subfolders.
                {
                    foreach (FolderItem recursedItem in recursiveFolderContents(item.Name, fileSuffix, contentType))
                    {
                        yield return recursedItem;
                    }

                    if (contentType != FolderContentType.FilesOnly)
                    {
                        yield return item;
                    }
                }
                else  // It's a file.
                {
                    if (contentType != FolderContentType.FoldersOnly)
                    {
                        if (wantAllFiles || string.Equals(Path.GetExtension(item.Name), fileSuffix, StringComparison.CurrentCultureIgnoreCase))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lists the items in a folder that match a specification, without having to return them all at once via an array.
        /// This is useful if there are a very large number of files in a folder.
        /// </summary>
        /// <param name="folderName">The name of a folder.</param>
        /// <param name="itemSpec">
        /// An item spec which may contain wildcards or which can be "", null, "*" or "*.*" for all items.
        /// </param>
        /// <param name="contentType">Type of the content to be returned.</param>
        /// <returns>An enumerator for all the items.</returns>

        public static IEnumerable<FolderItem> FolderContents(string folderName, string itemSpec, FolderContentType contentType)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!Directory.Exists(folderName))
            {
                //throw new DirectoryNotFoundException("Directory not found: " + folderName);
            }

            string spec;

            if (string.IsNullOrEmpty(itemSpec))
            {
                spec = Path.Combine(folderName, "*");
            }
            else
            {
                spec = Path.Combine(folderName, itemSpec);
            }

            WIN32_FIND_DATA findData;

            using (SafeFindHandle findHandle = FindFirstFile(spec, out findData))
            {
                if (!findHandle.IsInvalid)
                {
                    do
                    {
                        if ((findData.cFileName != ".") && (findData.cFileName != ".."))  // Ignore special "." and ".." folders.
                        {
                            switch (contentType)
                            {
                                case FolderContentType.FilesAndFolders:
                                    {
                                        if ((findData.dwFileAttributes & FileAttributes.Directory) == 0)
                                            filecnt++;
                                        else
                                            dircnt++;
                                        yield return new FolderItem(findData, folderName);
                                        break;
                                    }

                                case FolderContentType.FilesOnly:
                                    {
                                        if ((findData.dwFileAttributes & FileAttributes.Directory) == 0)
                                        {
                                            filecnt++;
                                            yield return new FolderItem(findData, folderName);
                                        }

                                        break;
                                    }

                                case FolderContentType.FoldersOnly:
                                    {
                                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                                        {
                                            dircnt++;
                                            yield return new FolderItem(findData, folderName);
                                        }

                                        break;
                                    }

                                default:
                                    {
                                        throw new ArgumentOutOfRangeException("contentType", contentType, "contentType is not one of the allowed values.");
                                    }
                            }
                        }
                    }
                    while (FindNextFile(findHandle, out findData));
                }
                else
                {
                    Debug.WriteLine("Cannot find files in " + spec, "Dmr.Common.IO.FileSystem.FolderContents()");
                }
            }
        }

        /// <summary>Used to safely wrap the handle used by FindFirstFile()/FindNextFile()/FindClose().</summary>

        internal sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            public SafeFindHandle()
                : base(true)
            {
                // Nothing to do.
            }

            protected override bool ReleaseHandle()
            {
                if (!IsInvalid && !IsClosed)
                {
                    return FindClose(this);
                }

                return (IsInvalid || IsClosed);
            }

            protected override void Dispose(bool disposing)
            {
                if (!IsInvalid && !IsClosed)
                {
                    FindClose(this);
                }

                base.Dispose(disposing);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
            public string cAlternate;
        }

        private const int MAX_PATH = 260;
        private const int MAX_ALTERNATE = 14;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFindHandle FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextFile(SafeHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindClose(SafeHandle hFindFile);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public long ToLong()
        {
            return dwLowDateTime + ((long)dwHighDateTime) << 32;
        }
    };

    /// <summary>Information about a file or folder obtained from FolderContents.</summary>

    public class FolderItem
    {

        /// <summary>Constructor.</summary>
        /// <param name="data">Data returned from FindFirstFile()/FindNextFile().</param>
        /// <param name="folder">The folder in which the file resides.</param>

        internal FolderItem(Program.WIN32_FIND_DATA data, string folder)
        {
            _data = data;
            _folder = folder;
        }

        /// <summary>The full path + filename of the item.</summary>

        public string Name
        {
            get
            {
                return Path.Combine(_folder, _data.cFileName);
            }
        }

        /// <summary>The full path + short 8.3 name of the item.</summary>

        public string ShortName
        {
            get
            {
                return Path.Combine(_folder, _data.cAlternate);
            }
        }

        /// <summary>Creation time of the item.</summary>

        public DateTime CreationTime
        {
            get
            {
                return DateTime.FromFileTime(_data.ftCreationTime.ToLong());
            }
        }

        /// <summary>Last access time of the item.</summary>

        public DateTime LastAccessTime
        {
            get
            {
                return DateTime.FromFileTime(_data.ftLastAccessTime.ToLong());
            }
        }

        /// <summary>Last write time of the item.</summary>

        public DateTime LastWriteTime
        {
            get
            {
                return DateTime.FromFileTime(_data.ftLastWriteTime.ToLong());
            }
        }

        /// <summary>Size of the item. Note: May exceed 32 bits in size.</summary>

        public long Size
        {
            get
            {
                return _data.nFileSizeLow + ((long)_data.nFileSizeHigh) << 32;
            }
        }

        /// <summary>Is the item's archive bit set?</summary>

        public bool IsArchive
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Archive) != 0);
            }
        }

        /// <summary>
        /// Is the item compressed?
        /// For a file, this means that all of the data in the file is compressed.
        /// For a directory, this means that compression is the default for newly created files and subdirectories.
        /// </summary>

        public bool IsCompressed
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Compressed) != 0);
            }
        }

        /// <summary>Reserved; do not use.</summary>

        public bool IsDevice
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Device) != 0);
            }
        }

        /// <summary>Is the item a folder? (If this is false, the item must be a file.)</summary>

        public bool IsFolder
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Directory) != 0);
            }
        }

        /// <summary>
        /// Is the item encrypted?
        /// For a file, this means that all data in the file is encrypted.
        /// For a directory, this means that encryption is the default for newly created files and subdirectories.
        /// </summary>

        public bool IsEncrypted
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Encrypted) != 0);
            }
        }

        /// <summary>Is the item hidden?</summary>

        public bool IsHidden
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Hidden) != 0);
            }
        }

        /// <summary>
        /// Only applicable to files.
        /// Is the file not to be indexed by the content indexing service?
        /// </summary>

        public bool IsNotContentIndexed
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.NotContentIndexed) != 0);
            }
        }

        /// <summary>
        /// Is the file data not available immediately?
        /// This attribute indicates that the file data is physically moved to offline storage.
        /// This attribute is used by Remote Storage, which is the hierarchical storage management software.
        /// </summary>

        public bool IsOffline
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Offline) != 0);
            }
        }

        /// <summary>Is the item read-only?</summary>

        public bool IsReadOnly
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.ReadOnly) != 0);
            }
        }

        /// <summary>Has the item got an associated reparse point?</summary>

        public bool IsReparsePoint
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.ReparsePoint) != 0);
            }
        }

        /// <summary>Only applicable to files. Is it a sparse file?</summary>

        public bool IsSparseFile
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.SparseFile) != 0);
            }
        }

        /// <summary>Is the item part of the operating system, or does the operating system use it exclusively?</summary>

        public bool IsSystem
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.System) != 0);
            }
        }

        /// <summary>
        /// Is The file being used for temporary storage?
        /// File systems attempt to keep all of the data in memory for quick access, rather than flushing it back to mass storage.
        /// </summary>

        public bool IsTemporary
        {
            get
            {
                return ((_data.dwFileAttributes & FileAttributes.Temporary) != 0);
            }
        }

        /// <summary>Only applicable to files. Is it a virtual file?</summary>

        public bool IsVirtual
        {
            get
            {
                return (((int)_data.dwFileAttributes & 0x10000) != 0);
            }
        }

        /// <summary>Convert to string - just returns the item's Name.</summary>
        /// <returns>The item's Name.</returns>

        public override string ToString()
        {
            return this.Name;
        }

        private Program.WIN32_FIND_DATA _data;
        private readonly string _folder;
    }
}
