# واجهات فرع الدراسات المركزية — دليل الواجهة الأمامية

كل الواجهات تحت `api/` وتحتاج رمز الدخول (`Authorization: Bearer …`). الأخطاء بالشكل المعتاد في النظام:
`{ statusCode, message }` (و`errors` لأخطاء التحقق). الرسائل عربية جاهزة للعرض.

- **400**: قاعدة عمل أو تحقق من المدخلات.
- **403**: لا صلاحية، أو السجل خارج فرعك.
- **404**: غير موجود.
- Swagger في بيئة التطوير: `/swagger`.

## قواعد عامة

- **الحدود:** كل صلاحيات الدراسات محصورة بدراسات فرع المستخدم، ومن يملك `ViewOrganizationDashboard` يرى كل الفروع.
- **الأعلام:** كل مخرَج يحمل أعلاماً جاهزة مثل `canEdit` و`canDelete` و`canSubmit` و`canCancel` و`canCreateAssignment`، وفي سطور التوقيع `canDecide`. أظهر الأزرار حسبها، ولا تعِد حساب القواعد في الواجهة.
- **التعدادات:** كل تعداد يأتي رقماً مع اسمه العربي (`status` و`statusName`). القوائم كلها في `GET api/StudyLookups/Enums`.
- **التعديل المتزامن:** أرسل `rowVersion` الذي استلمته مع كل تعديل. إن عدّل غيرك السجل بعد فتحه يعود 400 برسالة «عدّل مستخدم آخر…»، فأعد التحميل.
- **القوائم المقسّمة صفحات:** `PagedResultDto` = `{ items, totalCount, page, pageSize, totalPages }`، والحد الأقصى `pageSize ≤ 100`. فلاتر البحث المشتركة: `status`، `search`، `branchId` (لنظرة المؤسسة)، `from`، `to`، `page`، `pageSize`.
- **التواريخ:** التواريخ بلا وقت (`yyyy-MM-dd`).

## سير العمل

```text
طلب (REQ) ─Submit→ معتمد
  └─ تكليف IPA ─Submit→ رئيس الفرع ─→ معتمد
       └─ رد ─Submit→ القسم المتابع → التخطيط وإدارة المشاريع ─→ معتمد
            └─ أمر بدء PIO (إن لم تكن التوصية «غير متاح») ─Submit→ رئيس القسم المختص → رئيس الفرع ─→ معتمد
                 └─ مشروع: مراحل + أقسام مشاركة + فريق + مهام + مخططات
                      └─ RequestCompletion → إدارة المشاريع → رئيس الفرع ─→ مكتمل
```

- **حالات المستند:**
  - 1 مسودة؛
  - 2 بانتظار الاعتماد؛
  - 3 معتمد؛
  - 4 مرفوض: يُعدَّل فيعود مسودة ثم يُرسل من جديد؛
  - 5 ملغى: يبقى للأرشيف.
- **الحذف والإلغاء:** يُحذف المستند وهو مسودة فقط. المعتمد أو المعلّق يُلغى بسبب (`Cancel`)، بشرط ألا يكون له مستند تالٍ قائم.
- **التواقيع بالترتيب:**
  - الرفض من أي موقّع يرفض المستند، واكتمال الموافقات يعتمده.
  - لا يوقّع الشخص مرتين على المستند نفسه.
  - التوقيع بصفة «القسم المتابع/المختص/المرسل» لموظفي ذلك القسم فقط.
- **بانتظار توقيعي:** `GET api/StudyApprovals/Pending` يعيد ما يستطيع المستخدم توقيعه الآن. اعرضه كصندوق وارد.

## الواجهات

### القوائم — `api/StudyLookups`

| الطريقة | المسار | الصلاحية | ملاحظة |
|---|---|---|---|
| GET | `Enums` | ViewStudyLookups | كل التعدادات |
| GET/POST/PUT/DELETE | `StageTypes/GetAll?activeOnly`، `StageTypes/Create`، `StageTypes/Update/{id}`، `StageTypes/Delete/{id}` | View/Create/Edit/DeleteStudyLookup | `linkedDocument`: 0 لا شيء، 1 طلب، 2 تكليف، 3 رد، 4 أمر بدء |
| GET/POST/PUT/DELETE | `AttachmentTypes/…` بالنمط نفسه | العرض: أي صلاحية عرض للدراسات | |
| GET/POST/PUT/DELETE | `Disciplines/…` بالنمط نفسه | ViewStudyLookups… | |
| GET | `Employees?branchId&departmentId` | `AnyStudyEmployeePick` (أيٌّ من: Create/EditStudyResponse، Create/EditStudyStartOrder، EditStudyProject، AddStudyProjectMember) | موظفو فرع الدراسة لقوائم الاختيار — انظر تحت الجدول |

**`Employees`: قائمة اختيار الموظفين بلا `ViewUsers`:**

- **الاستعمال:** مدير المشروع المقترح في الرد، ومدير المشروع والمهندسون في أمر البدء، وتغيير مدير المشروع، وإضافة أعضاء القسم المشارك (مع `departmentId` القسم المشارك).
- **الحقول:** يعيد الموظفين **النشطين** فقط، بالحقول `id`، `fullName`، `departmentId`، `departmentName`، `officeId`، `officeName`. لا بريد ولا هاتف ولا رقم ذاتي ولا دور.
- **الترتيب:** بالقسم ثم الاسم.
- **الحد:** فرع المستخدم دائماً.
  - من يتصفح المؤسسة يمرّر `branchId` (فرع الدراسة) أو `departmentId`، ودونهما يعود 400.
  - فرع أو قسم خارج حدك = 403.
  - قسم ليس من الفرع المحدد = 400.
  - مستخدم بلا فرع = قائمة فارغة.
- **مطابقة الخادم:** ما في القائمة هو ما يقبله الخادم. مدير المشروع المقترح ومدير المشروع والمهندسون يجب أن يكونوا موظفين نشطين من فرع الدراسة، وإلا يعود 400 «… ليس من موظفي فرع الدراسة»، أو 404 للموظف غير النشط. والعضو يجب أن يكون من القسم المشارك نفسه.

### المستندات

المستندات هي `api/StudyRequests` و`api/StudyAssignments` و`api/StudyResponses` و`api/StudyStartOrders`، ولكل منها المسارات نفسها:

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `GetAll` (فلاتر البحث) | View… |
| GET | `Get/{id}` — التفاصيل مع `chain` (روابط السلسلة كلها) و`approvals` | View… |
| POST | `Create` | Create… |
| PUT | `Update/{id}` | Edit… |
| DELETE | `Delete/{id}` | Delete… |
| PUT | `Submit/{id}` | Edit… |
| PUT | `Cancel/{id}` بالجسم `{ "reason": "…" }` | CancelStudyDocuments |

- **الإنشاء يحمل معرّف المستند السابق:**
  - التكليف: `requestId`؛
  - الرد: `assignmentId`؛
  - أمر البدء: `responseId`.
- **أجزاء الرد وأمر البدء** تُرسل كاملة في كل حفظ وتُستبدل:
  - في الرد: `buildings` (سطر لكل نوع بناء، اثنان كحد أقصى)، `costItems`، `staffEstimates` (الاختصاص مرة واحدة)، `logisticItems`؛
  - في أمر البدء: `engineers` و`resources`.
- **الطلب:** تُرسل فيه `governorateCode` (SY01…SY14)، و`siteId` اختياري (من سجل المواقع)، و`branchId` لنظرة المؤسسة فقط.

### المشاريع — `api/StudyProjects`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `GetAll`، `Get/{id}` (الأقسام والأعضاء والمراحل ونسب التقدم والتواقيع) | ViewStudyProjects |
| POST | `Create` بالجسم `{ startOrderId, name, plannedStartDate, plannedEndDate, code? }` | CreateStudyProject |
| PUT | `Update/{id}` (`status`: 1 جارٍ أو 2 معلّق، و`managerUserId`) | EditStudyProject |
| DELETE | `Delete/{id}` (بلا مهام ولا مخططات ولا مرفقات) | DeleteStudyProject |
| PUT | `Cancel/{id}` | CancelStudyDocuments |
| PUT | `RequestCompletion/{id}` (كل المراحل مغلقة) | EditStudyProject |
| POST / DELETE | `Departments/{projectId}` بالجسم `{ departmentId }` / `Departments/{projectDepartmentId}` | Add/RemoveStudyProjectDepartment |
| POST / DELETE | `Members/{projectDepartmentId}` بالجسم `{ userId, roleTitle }` / `Members/{projectDepartmentId}/{userId}` | Add/RemoveStudyProjectMember (رئيس القسم لموظفي قسمه؛ انظر `departments[].canManageMembers`) |
| GET | `ActivityLog/{id}` | ViewStudyActivityLog |

- **مكاتب الأقسام المشاركة:** كل `departments[]` في التفاصيل يحمل `offices: [{ id, name }]` (مرتبة بالاسم، وقائمة فارغة لقسم بلا مكاتب). منها يُختار هدف «مكتب» في المهمة، بلا `ViewOffices`.
- **تفاصيل المشروع:** تأتي من `Get/{id}` ومن ردود كل التعديلات (الأقسام والأعضاء…)، أما `GetAll` فلا يحمل الأقسام.
- **`managerUserId`:** موظف نشط من فرع المشروع (من `StudyLookups/Employees`).

### المراحل — `api/StudyStages`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByProject/{projectId}` | ViewStudyProjects |
| POST | `Create` بالجسم `{ projectId, stageTypeId, weight, … }` | CreateStudyStage |
| PUT | `Update/{id}` (`status`: 1 أو 2) | EditStudyStage |
| PUT | `Reorder/{projectId}` بالجسم `{ stageIds: [...] }` | EditStudyStage |
| DELETE | `Delete/{id}` | DeleteStudyStage |
| PUT | `Close/{id}`، `Reopen/{id}` | CloseStudyStage |

- الإغلاق بالترتيب، وبعد إنجاز كل مهام المرحلة.
- مراحل المستندات (`linkedDocument ≠ 0`) تُنشأ مغلقة ولا تُحذف ولا يُعاد فتحها.

### المهام — `api/StudyTasks`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByStage/{stageId}` | ViewStudyProjects |
| GET | `ByProject/{projectId}`: كل مهام المشروع في طلب واحد (لمخطط Gantt) | ViewStudyProjects |
| GET | `Get/{id}` | من يرى المشاريع، أو المكلَّف |
| GET | `My?openOnly=true&page&pageSize` | UpdateStudyTaskProgress |
| POST | `Create` بالجسم `{ stageId, targetType, departmentId \| officeId \| assigneeUserId, title, weight, … }` | CreateStudyTask |
| PUT | `Update/{id}`، DELETE `Delete/{id}` | Edit/DeleteStudyTask |
| PUT | `Progress/{id}` بالجسم `{ progress, status }` | UpdateStudyTaskProgress (المكلَّف فقط؛ انظر `canUpdateProgress`) |

- **`ByProject`:** له حدّ `StudyStages/ByProject` نفسه، وأعلام `canEdit` و`canUpdateProgress` نفسها في `ByStage`.
  - الترتيب بترتيب المرحلة (`sortOrder`)، ثم البدء المخطط (المهام بلا بدء مخطط في آخر مرحلتها)، ثم الرقم.
  - كل مهمة تحمل `stageId` و`stageName` و`projectId` و`projectCode`، والتواريخ المخططة والفعلية.

قيم `targetType`:

- **1 قسم مشارك:** يُملأ `departmentId`.
- **2 مكتب من قسم مشارك:** يُملأ `officeId`.
- **3 عضو في الفريق:** يُملأ `assigneeUserId`.

### المخططات — `api/StudyDrawings`، و`api/StudyDesignApprovals`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `StudyDrawings/ByProject/{projectId}?kind`، `StudyDrawings/Get/{id}` (مع `versions` للمعماري) | ViewStudyDrawings |
| POST/PUT/DELETE | `StudyDrawings/Create` (`kind`: 1 معماري … 5 تقني)، `Update/{id}`، `Delete/{id}` | Create/Edit/DeleteStudyDrawing (يعدّل القسم المُصدِر) |
| PUT | `StudyDrawings/Review/{id}` بالجسم `{ approve, note }` (غير المعماري بانتظار المراجعة) | ReviewStudyDrawing |
| POST/PUT/DELETE | `StudyDrawings/Versions/{drawingId}`، `StudyDrawings/Version/{id}` | Create/Edit/DeleteArchitecturalVersion |
| GET/POST/PUT/DELETE | `StudyDesignApprovals/Get/{id}`، `Create` بالجسم `{ versionId, cdaDate, … }`، `Update/{id}`، `Delete/{id}` | ViewStudyDrawings / Create/Edit/DeleteDesignApproval |

- غير المعماري يُرسل `basedOnVersionId`، وهي نسخة معمارية معتمدة من المشروع نفسه.
- إصدار CDA ينشئ سطور التوقيع الثلاثة فوراً.

### التواقيع — `api/StudyApprovals`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `Pending` | أي صلاحية `SignStudyAs…` |
| GET | `ByOwner?ownerType&ownerId` | أي صلاحية عرض للدراسات |
| PUT | `Decide/{id}` بالجسم `{ decision, note }` | الصلاحية تُفحص لصفة السطر |

- **`ownerType`:** 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع، 10 CDA.
- **`decision`:** 2 معتمد، 3 معتمد مع ملاحظات، 4 مرفوض. الملاحظة مطلوبة للأخيرين.

### المرفقات — `api/StudyAttachments`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByOwner?ownerType&ownerId` | صلاحية عرض صاحبه |
| POST | `Upload?ownerType&ownerId&attachmentTypeId` — الجسم multipart فيه حقل ملف واحد `file` | صلاحية تعديل صاحبه |
| GET | `Get/{id}?download=true` (الصور وPDF تُعاين مباشرة؛ انظر `previewable`) | صلاحية عرض صاحبه |
| DELETE | `Delete/{id}` | صلاحية تعديل صاحبه |

- **`ownerType`:** 1 طلب، 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع، 8 مخطط، 9 نسخة معمارية.
- **ملفات المخططات بلا سقف للحجم** (جُرّب ملف 1GB). المعرّفات في عنوان الطلب لا في النموذج، كي تُفحص الصلاحية قبل استقبال الملف.
  ملف واحد لكل طلب رفع، و100 ملف لكل سجل. `size` بالبايت (رقم 64-بت).
- **في الواجهة:** ارفع بـ `FormData` فيه `file` فقط، وأظهر شريط التقدم (`reportProgress: true` في `HttpClient`) — رفع 1GB يأخذ وقتاً على الشبكة.
  التنزيل يحتاج ترويسة الدخول ويدعم `Range` (يُستأنف إن انقطع). للملفات الكبيرة اطلبه بـ `fetch` واكتبه مباشرة على القرص بدل تحميله في الذاكرة كـ Blob.
- **الصيغ المسموحة:** صور، PDF، Office، DWG/DXF، نصوص، أرشيف مضغوط. النوع يُكشف من محتوى الملف.
- **التخزين:** في مجلد المرفقات على الخادم دائماً. إن لم يُضبط المجلد يعود 400 برسالة تطلب إبلاغ مدير النظام.

### سجل الدراسة

`GET api/StudyActivityLogs/ByRequest/{requestId}` و`GET api/StudyProjects/ActivityLog/{id}` بصلاحية `ViewStudyActivityLog`. السجل مرتب الأحدث أولاً.

## الإشعارات (تصل عبر SignalR كالمعتاد)

| `type` | متى | `relatedEntityType` |
|---|---|---|
| `StudyApprovalRequested` | مستند بانتظار توقيع صفتك | `StudyAssignment` / `StudyResponse` / `StudyStartOrder` / `StudyDesignApproval` / `StudyProject` |
| `StudyDocumentDecided` | اعتُمد مستندك نهائياً أو رُفض | كما سبق |
| `StudyTaskAssigned` | مهمة جديدة لك أو لمكتبك أو قسمك | `StudyTask` |

## الصلاحيات في الواجهة

- **الأسماء:** في `Application/Common/AppPermissions.cs`، قسم «فرع الدراسات المركزية» (57 صلاحية). انسخها إلى `core/constants/access.ts`.
- **صفحة الأدوار:** أضف لها قسماً جديداً «الدراسات» بمجموعات: القوائم، الطلبات، التكليفات، الردود، أوامر البدء، التواقيع، المشاريع والفريق، المراحل والمهام، المخططات وCDA، السجل.
- **صلاحيات «أيٌّ من»:** `AnyStudySign`، `AnyStudyView`، `AnyStudyTaskView`، `AnyStudyAttachmentWrite`، `AnyStudyEmployeePick`.
