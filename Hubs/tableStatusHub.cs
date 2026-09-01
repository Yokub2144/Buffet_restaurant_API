using Microsoft.AspNetCore.SignalR;

namespace Buffet_Restaurant_Managment_System_API.Hubs
{
    public class tableStatusHub : Hub
    {
        // Client เรียกเพื่อ join group ดูโต๊ะ
        public async Task JoinTableRoom()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "TableRoom");
        }
        public async Task JoinBillRoom(string billId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Bill_{billId}");
        }
        public async Task LeaveTableRoom()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "TableRoom");
        }
        // 🟢 บรอดแคสต์ข้อมูลรายการคิดเงิน/QR ไปยัง Customer Display
        public async Task SendToCustomerDisplay(object data)
        {
            await Clients.All.SendAsync("ShowCustomerDisplay", data);
        }

        //  บรอดแคสต์คำสั่งล้างหน้าจอให้กลับเป็น Index
        public async Task ClearCustomerDisplay()
        {
            await Clients.All.SendAsync("ClearCustomerDisplay");
        }
        public async Task UpdateDiscount()
        {
            await Clients.All.SendAsync("UpdateDiscount");
        }
    }
}