using Microsoft.Win32;

var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ADDTClientSettings");

key.SetValue("IsBanned", false);
key.SetValue("BanReason", string.Empty);

key.Close();
