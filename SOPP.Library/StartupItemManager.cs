using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SOPP.Library
{
    namespace SimpleTools
    {
        /// <summary>
        /// 启动项管理器，支持多种启动项位置的查询和管理
        /// </summary>
        [SupportedOSPlatform("Windows")]
        public class StartupItemManager : IDisposable
        {
            #region 启动项位置定义

            private static readonly Dictionary<StartupLocation, (string? RegistryPath, string? FolderPath)> _locationPaths =
    new()
    {
    { StartupLocation.UserRegistry64, (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", null) },
    { StartupLocation.UserRegistry32, (@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", null) },
    { StartupLocation.SystemRegistry64, (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", null) },
    { StartupLocation.SystemRegistry32, (@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", null) },
    { StartupLocation.UserFolder, (null, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup")) },
    { StartupLocation.SystemFolder, (null, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        @"Programs\Startup")) }
};


            #endregion

            #region 私有字段

            private bool _isDisposed;
            private readonly RegistryKey _userRoot = Registry.CurrentUser;
            private readonly RegistryKey _systemRoot = Registry.LocalMachine;

            #endregion

            #region 构造函数和资源管理

            public StartupItemManager()
            {
                // 检查权限
                EnsureAdminPrivilegesIfNeeded();
            }

            private static void EnsureAdminPrivilegesIfNeeded()
            {
                // 检查是否需要管理员权限
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                if (!isAdmin && RequiresAdminPrivileges())
                {
                    throw new UnauthorizedAccessException("操作需要管理员权限");
                }
            }

            private static bool RequiresAdminPrivileges()
            {
                // 系统级别的启动项需要管理员权限
                return false; // 简化实现，实际应根据操作类型判断
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (_isDisposed)
                    return;

                if (disposing)
                {
                    _userRoot?.Dispose();
                    _systemRoot?.Dispose();
                }

                _isDisposed = true;
            }

            #endregion

            #region 公共方法

            /// <summary>
            /// 获取所有启动项
            /// </summary>
            /// <returns>启动项集合</returns>
            public IEnumerable<StartupItem> GetAllStartupItems()
            {
                var items = new List<StartupItem>();

                // 获取注册表启动项
                items.AddRange(GetRegistryStartupItems(StartupLocation.UserRegistry64));
                items.AddRange(GetRegistryStartupItems(StartupLocation.UserRegistry32));
                items.AddRange(GetRegistryStartupItems(StartupLocation.SystemRegistry64));
                items.AddRange(GetRegistryStartupItems(StartupLocation.SystemRegistry32));

                // 获取文件夹启动项
                items.AddRange(GetFolderStartupItems(StartupLocation.UserFolder));
                items.AddRange(GetFolderStartupItems(StartupLocation.SystemFolder));

                return items;
            }

            /// <summary>
            /// 移除启动项
            /// </summary>
            /// <param name="name">启动项名称</param>
            /// <param name="location">启动项位置</param>
            public void RemoveStartupItem(string name, StartupLocation location)
            {
                ValidateInput(name, null);

                if (IsRegistryLocation(location))
                {
                    RemoveRegistryStartupItem(name, location);
                }
                else
                {
                    RemoveFolderStartupItem(name, location);
                }
            }

            /// <summary>
            /// 检查启动项是否存在
            /// </summary>
            /// <param name="name">启动项名称</param>
            /// <param name="location">启动项位置</param>
            /// <returns>是否存在</returns>
            public bool StartupItemExists(string name, StartupLocation location)
            {
                ValidateInput(name, null);

                if (IsRegistryLocation(location))
                {
                    return RegistryStartupItemExists(name, location);
                }
                else
                {
                    return FolderStartupItemExists(name, location);
                }
            }

            #endregion

            #region 私有方法 - 注册表操作

            private List<StartupItem> GetRegistryStartupItems(StartupLocation location)
            {
                var items = new List<StartupItem>();
                var (_, _) = _locationPaths[location];

                using var key = OpenRegistryKey(location, RegistryKeyPermissionCheck.ReadSubTree);
                if (key == null)
                    return items;

                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        var value = key.GetValue(valueName) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            items.Add(new StartupItem
                            {
                                Name = valueName,
                                Path = value,
                                Location = location,
                                Architecture = GetArchitectureFromLocation(location),
                                ItemType = StartupItemType.Registry
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但继续处理其他项
                        System.Diagnostics.Debug.WriteLine($"获取注册表启动项 {valueName} 失败: {ex.Message}");
                    }
                }

                return items;
            }

            private void AddRegistryStartupItem(string name, string path, StartupLocation location)
            {
                using var key = OpenRegistryKey(location, RegistryKeyPermissionCheck.ReadWriteSubTree) ?? throw new InvalidOperationException($"无法打开注册表项: {_locationPaths[location].RegistryPath}");
                try
                {
                    key.SetValue(name, path, RegistryValueKind.String);
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException("需要管理员权限才能修改系统启动项");
                }
            }

            private void RemoveRegistryStartupItem(string name, StartupLocation location)
            {
                using var key = OpenRegistryKey(location, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key == null)
                    return;

                try
                {
                    if (key.GetValue(name) != null)
                    {
                        key.DeleteValue(name, false);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException("需要管理员权限才能修改系统启动项");
                }
            }

            private bool RegistryStartupItemExists(string name, StartupLocation location)
            {
                using var key = OpenRegistryKey(location, RegistryKeyPermissionCheck.ReadSubTree);
                if (key == null)
                    return false;

                return key.GetValue(name) != null;
            }

            private RegistryKey? OpenRegistryKey(StartupLocation location, RegistryKeyPermissionCheck permissionCheck)
            {
                var (registryPath, _) = _locationPaths[location];
                var isSystem = IsSystemLocation(location);
                var is64Bit = Is64BitLocation(location);

                var baseKey = isSystem ? _systemRoot : _userRoot;
                var view = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;

                try
                {
                    // 获取指定视图的基本注册表项
                    var baseKeyWithView = RegistryKey.OpenBaseKey(
                        isSystem ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                        view);

                    // 打开子键
                    var rights = permissionCheck == RegistryKeyPermissionCheck.ReadWriteSubTree
                        ? RegistryRights.QueryValues | RegistryRights.SetValue | RegistryRights.CreateSubKey
                        : RegistryRights.QueryValues;

                    return baseKeyWithView.OpenSubKey(registryPath!, RegistryKeyPermissionCheck.ReadWriteSubTree, rights);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"打开注册表项失败: {registryPath}, 错误: {ex.Message}");
                    return null;
                }
            }

            #endregion

            #region 私有方法 - 文件夹操作

            private static List<StartupItem> GetFolderStartupItems(StartupLocation location)
            {
                var items = new List<StartupItem>();
                var (_, folderPath) = _locationPaths[location];

                if (!Directory.Exists(folderPath))
                    return items;

                try
                {
                    var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => IsValidStartupFile(f));

                    foreach (var file in files)
                    {
                        try
                        {
                            items.Add(new StartupItem
                            {
                                Name = Path.GetFileNameWithoutExtension(file),
                                Path = file,
                                Location = location,
                                Architecture = Environment.Is64BitOperatingSystem ? Architecture.x64 : Architecture.x86,
                                ItemType = StartupItemType.File
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理启动项文件 {file} 失败: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取文件夹启动项失败: {folderPath}, 错误: {ex.Message}");
                }

                return items;
            }

            private static void RemoveFolderStartupItem(string name, StartupLocation location)
            {
                var (_, folderPath) = _locationPaths[location];

                if (!Directory.Exists(folderPath))
                    return;

                var shortcutPath = Path.Combine(folderPath, $"{name}.lnk");
                var filePath = Path.Combine(folderPath, name);

                try
                {
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                    else if (File.Exists(filePath) && IsValidStartupFile(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException("需要管理员权限才能修改系统启动项文件夹");
                }
            }

            private static bool FolderStartupItemExists(string name, StartupLocation location)
            {
                var (_, folderPath) = _locationPaths[location];

                if (!Directory.Exists(folderPath))
                    return false;

                var shortcutPath = Path.Combine(folderPath, $"{name}.lnk");
                var filePath = Path.Combine(folderPath, name);

                return File.Exists(shortcutPath) || (File.Exists(filePath) && IsValidStartupFile(filePath));
            }

            #endregion

            #region 辅助方法

            private static void ValidateInput(string name, string? path)
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name), "启动项名称不能为空");

                if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                    throw new FileNotFoundException("指定的文件不存在", path);
            }

            private static bool IsRegistryLocation(StartupLocation location)
            {
                return location == StartupLocation.UserRegistry64 ||
                       location == StartupLocation.UserRegistry32 ||
                       location == StartupLocation.SystemRegistry64 ||
                       location == StartupLocation.SystemRegistry32;
            }

            private static bool IsSystemLocation(StartupLocation location)
            {
                return location == StartupLocation.SystemRegistry64 ||
                       location == StartupLocation.SystemRegistry32 ||
                       location == StartupLocation.SystemFolder;
            }

            private static bool Is64BitLocation(StartupLocation location)
            {
                return location == StartupLocation.UserRegistry64 ||
                       location == StartupLocation.SystemRegistry64 ||
                       location == StartupLocation.UserFolder ||
                       location == StartupLocation.SystemFolder;
            }

            private static Architecture GetArchitectureFromLocation(StartupLocation location)
            {
                return Is64BitLocation(location) ? Architecture.x64 : Architecture.x86;
            }

            private static bool IsValidStartupFile(string filePath)
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return extension == ".exe" || extension == ".com" || extension == ".bat" ||
                       extension == ".cmd" || extension == ".vbs" || extension == ".lnk";
            }
            #endregion
        }

        #region 辅助类和枚举

        /// <summary>
        /// 启动项位置枚举
        /// </summary>
        public enum StartupLocation
        {
            UserRegistry64,
            UserRegistry32,
            SystemRegistry64,
            SystemRegistry32,
            UserFolder,
            SystemFolder
        }

        /// <summary>
        /// 启动项类型
        /// </summary>
        public enum StartupItemType
        {
            Registry,
            File
        }

        /// <summary>
        /// 架构类型
        /// </summary>
        public enum Architecture
        {
            x64,
            x86
        }

        /// <summary>
        /// 启动项信息类
        /// </summary>
        public struct StartupItem
        {
            /// <summary>
            /// 启动项名称
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 启动项路径
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// 启动项位置
            /// </summary>
            public StartupLocation Location { get; set; }

            /// <summary>
            /// 架构类型
            /// </summary>
            public Architecture Architecture { get; set; }

            /// <summary>
            /// 启动项类型
            /// </summary>
            public StartupItemType ItemType { get; set; }

            /// <summary>
            /// 获取启动项位置的友好名称
            /// </summary>
            public readonly string LocationName
            {
                get
                {
                    return Location switch
                    {
                        StartupLocation.UserRegistry64 => "用户注册表 (64位)",
                        StartupLocation.UserRegistry32 => "用户注册表 (32位)",
                        StartupLocation.SystemRegistry64 => "系统注册表 (64位)",
                        StartupLocation.SystemRegistry32 => "系统注册表 (32位)",
                        StartupLocation.UserFolder => "用户启动文件夹",
                        StartupLocation.SystemFolder => "系统启动文件夹",
                        _ => Location.ToString()
                    };
                }
            }
        }

        #endregion
    }
}