using Newtonsoft.Json;

namespace Server;

[Serializable]
public class Permission
{
    [JsonProperty("permission-set")]
    private IDictionary<string, bool> _permissions;

    [JsonConstructor]
    public Permission()
    {
        _permissions = new Dictionary<string, bool>();
    }

    public Permission(bool IsNormalCtor = true)
    {
        _permissions = new Dictionary<string, bool>
        {

            // setup base permissions

            { "ban", false },
            { "mute", false },
            { "store", false },
            { "say", true }
        };
    }

    public bool HasPermission(string perm)
    {
        if (!_permissions.ContainsKey(perm))
        {
            return false;
        }

        return _permissions[perm];
    }
    public void Give(string perm)
    {
        if (!_permissions.ContainsKey(perm))
        {
            _permissions.Add(perm, true);
            return;
        }
        _permissions[perm] = true;
    }
    public void Revoke(string perm)
    {
        if (!_permissions.ContainsKey(perm))
            return;

        _permissions[perm] = false;
    }
}
