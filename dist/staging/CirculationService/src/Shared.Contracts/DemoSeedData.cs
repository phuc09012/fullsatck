namespace Shared.Contracts;

public static class DemoSeedData
{
    public static class Users
    {
        public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        public static readonly Guid LibrarianId = Guid.Parse("11111111-1111-1111-1111-111111111102");
        public static readonly Guid Reader1Id = Guid.Parse("11111111-1111-1111-1111-111111111201");
        public static readonly Guid Reader2Id = Guid.Parse("11111111-1111-1111-1111-111111111202");
        public static readonly Guid Reader3Id = Guid.Parse("11111111-1111-1111-1111-111111111203");
    }

    public static class Books
    {
        public static readonly Guid CleanCodeId = Guid.Parse("22222222-2222-2222-2222-222222222201");
        public static readonly Guid HeadFirstDesignPatternsId = Guid.Parse("22222222-2222-2222-2222-222222222202");
        public static readonly Guid AspNetCoreInActionId = Guid.Parse("22222222-2222-2222-2222-222222222203");
        public static readonly Guid DomainDrivenDesignId = Guid.Parse("22222222-2222-2222-2222-222222222204");
        public static readonly Guid MicroservicesPatternsId = Guid.Parse("22222222-2222-2222-2222-222222222205");
    }

    public static class Borrowings
    {
        public static readonly Guid ActiveCleanCodeId = Guid.Parse("33333333-3333-3333-3333-333333333301");
        public static readonly Guid OverdueHeadFirstId = Guid.Parse("33333333-3333-3333-3333-333333333302");
        public static readonly Guid ReturnedAspNetCoreId = Guid.Parse("33333333-3333-3333-3333-333333333303");
    }
}
