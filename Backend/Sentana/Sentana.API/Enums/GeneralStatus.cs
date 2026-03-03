using System.Runtime.InteropServices;

namespace Sentana.API.Enums
{
    //chung cho building, service, account, contract
    public enum GeneralStatus : byte
    {
        Inactive = 0, // ngừng hoạt động / đã khóa / đã kết thúc
        Active = 1    // đang hoạt động / hiệu lực
    }
}