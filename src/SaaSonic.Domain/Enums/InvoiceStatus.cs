namespace SaaSonic.Domain.Enums;

public enum InvoiceStatus : short
{
    Draft=1,       
    Open=2,        
    Paid=3,      
    Cancelled=4,
    Overdue=5,
    Uncollectible=10
}
