# وثائق المعمارية حسب الوحدة

هذه الملفات تفاصيل CLAUDE.md السابقة، نُقلت **حرفياً** ورُتِّبت حسب الوحدة. هي مرجع ما يفعله كل جزء وقواعده الحالية وتاريخ قراراته.
لـ**لماذا** اتُّخذ قرار ما انظر [`docs/adr/`](../adr/README.md). للتشغيل في الإنتاج انظر [`docs/DEPLOYMENT.md`](../DEPLOYMENT.md).

| الوحدة | الملف |
|--------|-------|
| المصادقة والصلاحيات | [auth-and-permissions.md](auth-and-permissions.md) |
| الإجازات والتوقيعات الإلكترونية | [vacations-and-signatures.md](vacations-and-signatures.md) |
| تخزين المرفقات | [file-storage.md](file-storage.md) |
| التشغيل والإصدار | [running-and-release.md](running-and-release.md) |
| المكاتب والإشعارات | [organization-and-notifications.md](organization-and-notifications.md) |
| الواجهة (Angular) | [frontend.md](frontend.md) |
| مهام العمل ولوحات المتابعة | [work-tasks-and-dashboards.md](work-tasks-and-dashboards.md) |
| لوحة المهام المُسنَدة | [task-board.md](task-board.md) |
| توثيق الأجهزة | [device-inventory.md](device-inventory.md) |
| الخريطة وطبقات الفروع | [maps-and-layers.md](maps-and-layers.md) |
| الصيانة وقطع الغيار | [maintenance.md](maintenance.md) |
| مفكرتي | [todo-lists.md](todo-lists.md) |
| مركز التقارير | [reports-center.md](reports-center.md) |
| المشاريع والجداول الزمنية | [projects.md](projects.md) |
| الديوان: المعاملات الواردة والصادرة | [diwan.md](diwan.md) |
| الدراسات والمكاتب الفنية: أوامر الدراسة ومخرجات المكاتب والإرسال للمركزية | [studies.md](studies.md) |
| بوابات الجودة والاختبارات | [quality-gates.md](quality-gates.md) |

## كيف نحافظ عليها

- عدّل ملف الوحدة **مع الكود** في الـ commit نفسه، لا CLAUDE.md.
- وحدة جديدة = ملف جديد هنا + صف في الجدول أعلاه وفي جدول CLAUDE.md.
- المسارات المذكورة لملفات الواجهة قد تسبق إعادة هيكلتها (`features/<f>/pages|components|data-access|utils`): الاسم صحيح والمجلد قد يختلف؛ المرجع في مستودع الواجهة `docs/ARCHITECTURE.md`.
