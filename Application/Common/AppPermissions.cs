namespace Application.Common
{
    /// <summary>
    /// المصدر الوحيد لكل صلاحيات النظام (قرار المستخدم 2026-09-30: صلاحية منفصلة لكل عملية).
    /// - DbSeeder يضيفها لقاعدة البيانات ويمنحها كلها للسوبر ادمن.
    /// - AddAPIRigstrationServices يسجّل سياسة (Policy) بنفس الاسم لكل واحدة.
    /// لإضافة صلاحية: أضفها هنا فقط ثم استخدم اسمها في [Authorize(Policy = "...")].
    /// لإعادة تسمية/حذف صلاحية: تحتاج migration (الـ seeder يضيف فقط) — راجع Split_All_Permissions.
    ///
    /// كل قراءة لها صلاحية "عرض" أيضاً (قرار المستخدم 2026-09-30)، حتى القوائم التي تملأ النماذج:
    /// من يقدّم إجازة يحتاج ViewVacationTypes، ومن يسجّل طلب صيانة يحتاج ViewMaintenanceLookups،
    /// ومن يختار قسماً/فرعاً في أي نموذج يحتاج ViewDepartments — امنحها مع صلاحية الإنشاء/التعديل المقابلة.
    /// وطلب الصيانة يُربط بجهاز موجود: من يقدّم أو يعدّل طلباً يحتاج ViewMaintenanceDevices (وإضافة جهاز جديد CreateMaintenanceDevice).
    /// </summary>
    public static class AppPermissions
    {
        public sealed record Definition(string Name, string Description);

        public static readonly IReadOnlyList<Definition> All =
        [
            new("ViewUsers",   "عرض المستخدمين"),
            new("CreateUser",  "إضافة مستخدم"),
            new("EditUser",    "تعديل مستخدم"),
            new("ToggleUserActive", "تفعيل حسابات الموظفين وتعطيلها (دون تعديل بياناتهم)"),
            new("DeleteUser",  "حذف مستخدم"),

            new("ViewBranches", "عرض الفروع"),
            new("CreateBranch", "إضافة فرع"),
            new("EditBranch",   "تعديل فرع"),
            new("DeleteBranch", "حذف فرع"),

            new("ViewDepartments",  "عرض الأقسام"),
            new("CreateDepartment", "إضافة قسم"),
            new("EditDepartment",   "تعديل قسم"),
            new("DeleteDepartment", "حذف قسم"),

            new("ViewOffices",  "عرض المكاتب"),
            new("CreateOffice", "إضافة مكتب"),
            new("EditOffice",   "تعديل مكتب"),
            new("DeleteOffice", "حذف مكتب"),

            new("ViewRoles",  "عرض الأدوار والصلاحيات"),
            new("CreateRole", "إضافة دور"),
            new("EditRole",   "تعديل دور وصلاحياته"),
            new("DeleteRole", "حذف دور"),

            // كل صلاحية تحمل حدّها الثابت في معناها، ويطبّقه الـ handler (قرار المستخدم 2026-10-03: Role-Permission فقط، بلا نطاقات)
            new("ViewVacations",           "عرض إجازاتي"),
            new("ViewDepartmentVacations", "عرض إجازات موظفي قسمه وإحصائياتها"),
            new("ViewBranchVacations",     "عرض إجازات موظفي فرعه وإحصائياتها"),
            new("CreateVacation",          "تقديم طلب إجازة"),
            new("CancelVacation",          "إلغاء طلب إجازة"),
            new("ApproveVacationFirst",    "الموافقة الأولى على إجازات موظفي فرعه أو رفضها"),
            new("ApproveVacationFinal",    "الاعتماد النهائي لإجازات موظفي فرعه أو رفضها"),
            new("PrintVacation",           "طباعة نموذج طلب الإجازة (للإجازات التي يستطيع عرضها)"),

            new("ViewVacationTypes",  "عرض أنواع الإجازات"),
            new("CreateVacationType", "إضافة نوع إجازة"),
            new("EditVacationType",   "تعديل نوع إجازة"),
            new("DeleteVacationType", "حذف نوع إجازة"),

            // العطل الرسمية: لا تُحسب من مدة الإجازة (مع الجمعة)
            new("ViewHolidays",  "عرض العطل الرسمية"),
            new("CreateHoliday", "إضافة عطلة رسمية"),
            new("EditHoliday",   "تعديل عطلة رسمية"),
            new("DeleteHoliday", "حذف عطلة رسمية"),

            // لوحات المتابعة: لوحة وحدة المستخدم وما تحتها (لا لوحة بلا صلاحية، حتى الشخصية)
            new("ViewOrganizationDashboard", "لوحة نظرة عامة على المؤسسة كلها (كل الفروع)"),
            new("ViewMyDashboard",           "لوحتي الشخصية في لوحة المتابعة"),
            new("ViewBranchDashboard",     "لوحة متابعة فرعه وأقسامه ومكاتبه"),
            new("ViewDepartmentDashboard", "لوحة متابعة قسمه ومكاتبه"),
            new("ViewOfficeDashboard",     "لوحة متابعة مكتبه"),
            new("ViewBranchMap",           "عرض خريطة سوريا ببيانات فرعه في لوحة المتابعة"),

            // لوحة المهام المُسندة: ViewTaskBoard تفتح اللوحة (واستقبال ما أُسند للمستخدم شخصياً)، والإسناد والتولّي بصلاحياتهما
            new("ViewTaskBoard",          "الوصول إلى لوحة المهام واستقبال المهام المسندة إليه شخصياً"),
            new("AssignTaskToDepartment", "إسناد مهام لأقسام فرعه"),
            new("AssignTaskToOffice",     "إسناد مهام لمكاتب قسمه"),
            new("AssignTaskToUser",       "إسناد مهام لموظفي مكتبه"),
            new("HandleUnitTasks",        "تولّي المهام المسندة لوحدته (تغيير حالتها وتفويضها)"),
            new("ViewTaskStats",          "عرض إحصائيات المهام ضمن نطاقه (الإنجاز في الموعد والمتأخرات لكل جهة)"),

            new("ViewMyWorkTasks", "عرض مهام العمل المسندة إليه وصفحاتها"),
            new("ViewWorkTasks",  "عرض مهام العمل وإسنادها"),

            new("ViewNotifications", "عرض الإشعارات واستلامها (الجرس والتنبيهات اللحظية)"),
            new("CreateWorkTask", "إضافة مهمة عمل"),
            new("EditWorkTask",   "تعديل مهمة عمل وإسنادها"),
            new("DeleteWorkTask", "حذف مهمة عمل"),

            // المواقع (2026-10-11): سجل مشترك فريد بين كل الفروع، تُبنى عليه خرائط الفروع — الحد: كل المواقع
            new("ViewSites",  "عرض سجل المواقع المشترك (كل المواقع)"),
            new("CreateSite", "إضافة موقع إلى السجل المشترك"),
            new("EditSite",   "تعديل بيانات أي موقع (الاسم والإحداثيات تظهر لكل الفروع)"),
            new("DeleteSite", "حذف موقع (إن لم يكن عليه تركيبات أو بيانات لأي فرع)"),

            // طبقات خرائط الفروع (1.2.0): الطبقة وحقولها ملك الفرع — الحد: طبقات فرعي
            new("ViewMapLayers",  "عرض طبقات خريطة فرعي وحقولها"),
            new("CreateMapLayer", "إنشاء طبقة في خريطة فرعي وتعريف حقولها"),
            new("EditMapLayer",   "تعديل طبقات فرعي وحقولها (إضافة حقل، إخفاؤه أو حذفه)"),
            new("DeleteMapLayer", "حذف طبقة من خريطة فرعي (إن لم تكن عليها سجلات)"),
            // سجلات الطبقات: كل سجل يحمل قسم من أدخله — الحد: سجلات قسمي، أو فرعي كله مع ViewBranchMapEntries
            new("ViewDepartmentMapEntries", "عرض سجلات طبقات فرعي التي أدخلها قسمي"),
            new("ViewBranchMapEntries",     "عرض سجلات طبقات فرعي كلها (كل الأقسام) وإدارتها ضمن صلاحياتي"),
            new("CreateMapEntry", "إضافة سجل طبقة على موقع (يُسجَّل باسم قسمي)"),
            new("EditMapEntry",   "تعديل سجلات الطبقات التي أراها"),
            new("DeleteMapEntry", "حذف سجلات الطبقات التي أراها"),

            // توثيق الأجهزة (كتالوج أجهزة مشترك / تركيبات) — العرض متاح أيضاً لمن يملك الإضافة أو التعديل أو الحذف (AnyOf).
            // التركيب يُسجَّل باسم قسم من أضافه وفرعه (2026-10-11): الحد = تركيبات قسمي، أو فرعي مع ViewBranchDevices
            new("ViewDevices",  "عرض كتالوج الأجهزة وتركيبات قسمي وسجلها (بلا كلمات السر)"),
            new("ViewBranchDevices", "عرض تركيبات الأجهزة في فرعي كله وإدارتها ضمن صلاحياتي (لا قسمي فقط)"),
            new("CreateDevice", "إضافة أجهزة للكتالوج وتركيبات (تُسجَّل باسم قسمي) واستيرادها من Excel"),
            new("EditDevice",   "تعديل الأجهزة والتركيبات التي أراها"),
            new("DeleteDevice", "حذف الأجهزة والتركيبات التي أراها"),
            new("RevealDevicePasswords", "إظهار كلمات سر التركيبات التي أراها ونسخها (يُسجَّل كل إظهار)"),

            new("ViewMaintenanceTasks",  "عرض مهام الصيانة"),
            new("CreateMaintenanceTask", "إضافة مهمة صيانة"),
            new("EditMaintenanceTask",   "تعديل مهمة صيانة"),
            new("DeleteMaintenanceTask", "حذف مهمة صيانة"),
            new("AssignMaintenanceTask", "توجيه مهمة صيانة أو نقلها إلى موظف من قسمه"),

            new("ViewDepartmentMaintenance", "الاطلاع على سجلات صيانة قسمه (وتعديلها وحذفها لمن يملك صلاحيتهما)"),

            new("ViewMaintenanceRequests",  "عرض طلبات الصيانة والبحث فيها"),
            new("CreateMaintenanceRequest", "تقديم طلب صيانة"),
            new("EditMaintenanceRequest",   "تعديل بيانات طلب صيانة (وحالته عند التعديل تحتاج صلاحية تغيير الحالة)"),
            new("ChangeMaintenanceStatus",  "تغيير حالة طلب صيانة (دون تعديل بياناته)"),
            new("DeleteMaintenanceRequest", "حذف طلب صيانة"),
            new("ViewMaintenanceStats",     "عرض إحصائيات الصيانة (طلبات ومهام: سجلاته وسجلات قسمه لمن يملك الاطلاع على القسم)"),
            new("AssignMaintenanceRequest", "نقل طلب صيانة من قسمه إلى موظف آخر من قسمه"),
            new("ViewMyMaintenanceRequests", "متابعة طلبات صيانة أجهزته (هو عميلها) وحالتها"),
            new("RequestMaintenanceTransfer", "طلب تحويل طلب صيانة مسند إليه إلى موظف آخر (يقرّره رئيس القسم)"),
            new("SignMaintenanceReceipt",   "توقيع أوراق تسليم طلبات صيانة قسمه (اسمه وتوقيعه في الطباعة)"),

            // أجهزة الصيانة: سجل مشترك لمن يملك الصلاحية (تُمنح لموظفي قسم الصيانة) — بلا حدّ قسم
            new("ViewMaintenanceDevices",  "عرض أجهزة الصيانة والبحث فيها بالرقم التسلسلي"),
            new("CreateMaintenanceDevice", "إضافة جهاز صيانة"),
            new("EditMaintenanceDevice",   "تعديل بيانات جهاز صيانة"),
            new("DeleteMaintenanceDevice", "حذف جهاز صيانة (إن لم تكن له طلبات)"),

            // قوائم المهام الشخصية (ToDoList): كل صلاحية على قوائمه هو فقط (والسوبر ادمن كلها)
            new("ViewToDoLists",  "عرض قوائم مهامه الشخصية"),
            new("CreateToDoList", "إضافة قائمة مهام شخصية"),
            new("EditToDoList",   "تعديل قوائم مهامه الشخصية"),
            new("DeleteToDoList", "حذف قوائم مهامه الشخصية"),

            // التوقيع الإلكتروني: رفع توقيعي وتغييره بكلمة المرور، ويُحفظ مع قراراتي الموقَّعة (الاعتماد النهائي للإجازة والرفض)
            new("ManageMySignature", "رفع توقيعه الإلكتروني وتغييره (يُطلب تأكيد كلمة المرور)"),

            // أجهزة الصيانة: سجل مشترك لمن يملك الصلاحية (تُمنح لموظفي قسم الصيانة) — بلا حدّ قسم
            new("ViewMaintenanceDevices",  "عرض أجهزة الصيانة والبحث فيها بالرقم التسلسلي"),
            new("CreateMaintenanceDevice", "إضافة جهاز صيانة"),
            new("EditMaintenanceDevice",   "تعديل بيانات جهاز صيانة"),
            new("DeleteMaintenanceDevice", "حذف جهاز صيانة (إن لم تكن له طلبات)"),

            // مخزون قطع الغيار: مخزون لكل قسم — كل صلاحية حدّها قطع قسمه (مدير النظام: كل الأقسام)
            new("ViewSpareParts",        "عرض قطع غيار مخزون قسمه وحركاتها"),
            new("CreateSparePart",       "إضافة قطعة غيار إلى مخزون قسمه"),
            new("EditSparePart",         "تعديل بيانات قطعة غيار في مخزون قسمه"),
            new("DeleteSparePart",       "حذف قطعة غيار من مخزون قسمه (إن لم تكن لها حركات)"),
            new("ReceiveSpareParts",     "إدخال قطع غيار إلى مخزون قسمه (ويصله تنبيه نزول القطعة تحت حدها الأدنى)"),
            new("AdjustSparePartStock",  "تسوية رصيد قطعة غيار في مخزون قسمه (جرد أو تالف)"),
            new("IssueSparePart",        "صرف قطع غيار على طلبات الصيانة وإعادتها (طلباته، وطلبات قسمه لمن يملك الاطلاع على القسم)"),
            new("ViewSparePartReports",  "تقارير قطع غيار قسمه: أكثر القطع صرفاً وتكلفة الأجهزة"),

            new("ViewMaintenanceLookups", "عرض أنواع الأجهزة والشركات والأعطال وحالات الطلب"),
            new("CreateMaintenanceLookup", "إضافة أنواع الأجهزة والشركات والأعطال وحالات الطلب"),
            new("EditMaintenanceLookup",   "تعديل أنواع الأجهزة والشركات والأعطال وحالات الطلب"),
            new("DeleteMaintenanceLookup", "حذف أنواع الأجهزة والشركات والأعطال وحالات الطلب"),
        ];

        /// <summary>
        /// صلاحيات تغيّر بنية الفروع — لا يمنحها إلا السوبر ادمن (منع تصعيد الصلاحيات:
        /// من يدير المستخدمين أو الأدوار داخل فرعه لا يستطيع منح نفسه أو غيره التحكم بالفروع).
        /// </summary>
        public static bool IsBranchManagement(string permissionName) =>
            permissionName is "CreateBranch" or "EditBranch" or "DeleteBranch";

        /// <summary>
        /// سياسات "أيّ من" لواجهات تقبل أكثر من صلاحية (لا صفوف لها في قاعدة البيانات): من يملك واحدة منها يدخل،
        /// وحدّ كل صلاحية على السجلات يطبّقه الـ handler. مدير النظام يملك كل الصلاحيات فيمرّ دائماً.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> AnyOf = new Dictionary<string, string[]>
        {
            ["AnyDepartmentDashboard"] = ["ViewBranchDashboard", "ViewDepartmentDashboard"],
            ["AnyOfficeDashboard"] = ["ViewBranchDashboard", "ViewDepartmentDashboard", "ViewOfficeDashboard"],
            ["AnyVacationView"] = ["ViewVacations", "ViewDepartmentVacations", "ViewBranchVacations", "ApproveVacationFirst", "ApproveVacationFinal"],
            ["AnyVacationApprove"] = ["ApproveVacationFirst", "ApproveVacationFinal"],
            ["AnyVacationStats"] = ["ViewDepartmentVacations", "ViewBranchVacations"],
            ["AnyMaintenanceAssign"] = ["AssignMaintenanceRequest", "AssignMaintenanceTask"],
            ["AnyMaintenanceRequestWrite"] = ["CreateMaintenanceRequest", "EditMaintenanceRequest"],
            ["AnyWorkTaskView"] = ["ViewMyWorkTasks", "ViewWorkTasks", "ViewBranchDashboard", "ViewDepartmentDashboard", "ViewOfficeDashboard"],
            // توثيق الأجهزة: من يضيف أو يعدّل أو يحذف يرى ما يعمل عليه
            ["AnyDeviceView"] = ["ViewDevices", "ViewBranchDevices", "CreateDevice", "EditDevice", "DeleteDevice"],
            // قراءة سجل المواقع: لمن يدير المواقع، ولمن يوثّق الأجهزة أو يسجّل معلومات الطبقات (يختار الموقع في النموذج)
            // قراءة طبقات فرعي وحقولها: لمن يديرها، ولمن يعمل على سجلاتها (يحتاج حقول الطبقة)
            ["AnyMapLayerRead"] = ["ViewMapLayers", "CreateMapLayer", "EditMapLayer", "DeleteMapLayer",
                "ViewDepartmentMapEntries", "ViewBranchMapEntries", "CreateMapEntry", "EditMapEntry", "DeleteMapEntry"],
            ["AnyMapEntryView"] = ["ViewDepartmentMapEntries", "ViewBranchMapEntries", "CreateMapEntry", "EditMapEntry", "DeleteMapEntry"],
            // رفع ملفات حقول «ملفات» (معلّقة حتى حفظ السجل): لمن يضيف السجلات أو يعدّلها
            ["AnyMapEntryWrite"] = ["CreateMapEntry", "EditMapEntry"],
            ["AnySiteView"] = ["ViewSites", "CreateSite", "EditSite", "DeleteSite", "ViewDevices", "ViewBranchDevices", "CreateDevice", "EditDevice", "DeleteDevice",
                "ViewDepartmentMapEntries", "ViewBranchMapEntries", "CreateMapEntry", "EditMapEntry", "DeleteMapEntry"],
        };

        // أسماء الصلاحيات التي تفحصها الـ handlers نفسها (لتحديد السجلات) — ثوابت بدل نصوص متكررة
        public const string ViewVacations = "ViewVacations";
        public const string ViewDepartmentVacations = "ViewDepartmentVacations";
        public const string ViewBranchVacations = "ViewBranchVacations";
        public const string ApproveVacationFirst = "ApproveVacationFirst";
        public const string ApproveVacationFinal = "ApproveVacationFinal";
        public const string ViewBranchDashboard = "ViewBranchDashboard";
        public const string ViewDepartmentDashboard = "ViewDepartmentDashboard";
        public const string ViewOfficeDashboard = "ViewOfficeDashboard";
        public const string ViewBranchMap = "ViewBranchMap";
        public const string ViewBranchDevices = "ViewBranchDevices";
        public const string ViewBranchMapEntries = "ViewBranchMapEntries";
        public const string ViewTaskBoard = "ViewTaskBoard";
        public const string AssignTaskToDepartment = "AssignTaskToDepartment";
        public const string AssignTaskToOffice = "AssignTaskToOffice";
        public const string AssignTaskToUser = "AssignTaskToUser";
        public const string HandleUnitTasks = "HandleUnitTasks";
        public const string ViewTaskStats = "ViewTaskStats";
        public const string ViewDepartmentMaintenance = "ViewDepartmentMaintenance";
        public const string ChangeMaintenanceStatus = "ChangeMaintenanceStatus";
        public const string ViewMaintenanceStats = "ViewMaintenanceStats";
        public const string ToggleUserActive = "ToggleUserActive";
        public const string ViewWorkTasks = "ViewWorkTasks";
        public const string ViewOrganizationDashboard = "ViewOrganizationDashboard";
        public const string ViewMyDashboard = "ViewMyDashboard";
        public const string ViewMyWorkTasks = "ViewMyWorkTasks";
        public const string ViewNotifications = "ViewNotifications";
        public const string SignMaintenanceReceipt = "SignMaintenanceReceipt";
        public const string IssueSparePart = "IssueSparePart";
        public const string ReceiveSpareParts = "ReceiveSpareParts";
    }
}
