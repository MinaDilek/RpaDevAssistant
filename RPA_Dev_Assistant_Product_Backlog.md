# RPA Dev Assistant – Product Backlog & Implementation Checklist

> Bu belge, RPA Dev Assistant projesinde planlanan özellikleri takip etmek için hazırlanmıştır.
> Codex bu dosyayı repository içinde okuyabilir, mevcut implementasyonu inceleyebilir ve gerçekten tamamlanmış maddeleri `[x]` olarak işaretleyebilir.

## Codex için kullanım talimatı

Bu dosyayı proje backlog/checklist kaynağı olarak kullan.

Kurallar:

- Repository içindeki mevcut kodu, testleri, frontend/backend yapısını ve dokümantasyonu incele.
- Bir maddeyi yalnızca gerçekten implement edilmiş ve çalışır durumda olduğuna dair kod/test/entegrasyon kanıtı varsa `[x]` yap.
- Kısmen yapılmış maddeleri `[ ]` bırak ve sonuna `— Kısmi: ...` şeklinde kısa not ekle.
- Sadece dosya veya sınıf adı var diye bir özelliği tamamlanmış sayma.
- Mock/sample seviyesindeki özellikleri production-ready olarak işaretleme.
- Mevcut özellikleri yeniden implement etme.
- Her inceleme sonunda:
  - Tamamlanan maddeleri işaretle.
  - Kısmi maddelere kısa durum notu ekle.
  - Yeni tespit edilen teknik borç veya eksik özellikleri en alttaki `Yeni Tespitler` bölümüne ekle.
- Bu belgeyi güncellerken başlık yapısını koru.

---

## 1. Analyzer & Code Quality

- [x] Gerçek UiPath projelerinde validation ve false-positive audit
- [x] Rule set’in genişletilmesi
- [ ] UiPath Windows / Windows-Legacy / Modern / Classic proje desteğinin sağlamlaştırılması — Kısmi: compatibility metadata okunuyor ve legacy UI rule var; tüm proje tipleri için kapsamlı davranış matrisi yok.
- [x] Workflow complexity analizi
- [x] Nesting depth analizi
- [x] Büyük workflow tespiti
- [x] Hard-coded değer tespiti
- [ ] Hard-coded URL / dosya yolu tespiti — Kısmi: absolute/UNC dosya yolu tespiti var; hard-coded URL kuralı yok.
- [x] Credential / secret güvenlik kontrolleri
- [x] Selector kalite analizi
- [x] `idx` ve kırılgan selector kontrolleri
- [x] Timeout / ContinueOnError kontrolleri
- [ ] Retry Scope / Check App State kullanım analizi — Kısmi: Delay rule önerilerinde state-based wait geçiyor; Retry Scope/Check App State kullanımını ölçen ayrı analiz yok.
- [x] Exception handling kalite analizi
- [ ] BusinessRuleException kullanım analizi
- [x] Logging kalite analizi
- [ ] Argument naming convention kontrolleri
- [ ] Variable naming convention kontrolleri
- [ ] Kullanılmayan argument/variable tespiti
- [ ] Invoke Workflow argument mapping kontrolleri
- [x] Skor sisteminin gerçek projelere göre kalibre edilmesi

## 2. Workflow Intelligence

- [x] Workflow detay ekranı
- [x] Activity hierarchy/tree görünümü
- [x] Workflow bazlı Findings ekranı
- [x] Workflow bazlı AI Review
- [x] Workflow invocation graph
- [ ] Project Architecture görselleştirmesi
- [x] Caller / Callee analizi
- [ ] Kullanılmayan workflow tespiti
- [ ] Circular workflow reference tespiti
- [ ] REFramework detaylı uygunluk analizi — Kısmi: REFramework detection var; detaylı compliance/checklist analizi yok.
- [ ] Queue kullanım analizi
- [ ] Config kullanım analizi
- [x] Flowchart tabanlı workflow’ları Sequence/standart workflow yapısına dönüştürme — Safe olarak sınıflanan root Flowchart workflow için controlled apply + backup + rollback; standalone XAML akışında ise orijinali değiştirmeden yeni Sequence dosyası olarak kaydetme destekleniyor.
- [x] Flowchart → Workflow dönüşüm önizlemesi — Workflow detail ve bağımsız Flowchart Converter sayfasında destekleniyor.
- [x] Flowchart dönüşümünde branch/decision mantığının korunması — FlowDecision, FlowSwitch ve shared continuation davranışı regression testleriyle korunuyor.

## 3. Dependency & Package Analysis

- [x] Dependency/package analizi
- [ ] Eski UiPath package tespiti — Kısmi: offline legacy package indicator var; güncel/deprecated package metadata kaynağı yok.
- [x] Uyumsuz package versiyonlarının tespiti
- [x] Kullanılmayan dependency tespiti
- [x] Modern / Classic activity uyumsuzluk analizi
- [ ] Package version risk analizi — Kısmi: declared-version alignment riski var; latest/vulnerability/deprecation kontrolü yok.

## 4. AI Features

- [ ] AI Project Review kalitesinin geliştirilmesi — Kısmi: OpenAI provider, structured prompt/response ve redacted context var; kalite/eval kalibrasyonu yok.
- [x] Workflow bazlı AI Review’un geliştirilmesi
- [x] Ask Project yeteneklerinin genişletilmesi
- [x] Evidence/citation sisteminin geliştirilmesi
- [x] AI cevaplarının güven skorunun geliştirilmesi
- [ ] AI cevaplarında evidence ve interpretation ayrımının iyileştirilmesi — Kısmi: evidence alanları ve prompt guardrails var; UI/contract seviyesinde ayrım tam değil.
- [x] Türkçe AI Review desteği
- [x] Türkçe Ask Project desteğinin geliştirilmesi
- [ ] Local LLM provider desteği
- [ ] AI provider seçimi

## 5. Fix & Refactoring

- [x] Fix Suggestion doğruluğunun artırılması
- [x] Patch Preview geliştirmeleri
- [x] Güvenli Apply Fix
- [ ] Apply All Fix
- [x] Fix öncesi backup
- [x] Fix sonrası rollback
- [x] Project/file hash ile stale-change kontrolü
- [x] Safe Automatic Fix sınıflandırması
- [x] DisplayName otomatik düzeltme
- [ ] Workflow rename preview — Kısmi: RPA006 naming suggestion var; workflow rename diff/rename impact preview yok.
- [ ] Workflow rename sırasında Invoke Workflow referanslarının güncellenmesi
- [x] Flowchart → Sequence dönüşüm preview — Workflow detail ve standalone XAML seçimi için doğrulandı.
- [x] Flowchart → Sequence kontrollü dönüşüm — Safe conversion apply, stale hash kontrolü, backup/rollback ve standalone Save As akışıyla doğrulandı.
- [ ] Refactoring önerileri — Kısmi: AI/manual fix suggestions genel refactoring önerisi üretebiliyor; ayrı refactoring motoru yok.

## 6. Localization & UX

- [x] Türkçe / İngilizce dil desteği
- [x] Bulguların Türkçeleştirilmesi
- [x] Recommendation metinlerinin Türkçeleştirilmesi
- [x] Fix Suggestion metinlerinin Türkçeleştirilmesi
- [x] AI Review çıktılarının seçili dile göre üretilmesi
- [x] Workflow sayısı kartının tıklanabilir olması
- [x] Finding sayısı kartlarının tıklanabilir olması
- [x] Workflow arama
- [x] Workflow filtreleme
- [x] Finding filtreleme
- [ ] Finding arama
- [ ] Finding severity sıralaması — Kısmi: Top Issues severity sıralı; tüm Findings ekranında explicit sort kontrolü yok.
- [x] Workflow risk göstergesi
- [ ] Activity tree içerisinde arama
- [ ] Büyük projelerde performans iyileştirmeleri

## 7. Reports

- [x] HTML rapor geliştirmeleri
- [x] PDF rapor export — `PdfUiPathReportExporter` ile C# tarafında saf PDF-1.4 üretimi, unit testler, `/api/uipath/projects/report` endpoint ve frontend dışa aktarım desteği doğrulandı.
- [ ] Yönetici özeti — Kısmi: report summary/counts var; narrative executive summary yok.
- [x] Teknik detay raporu
- [x] Workflow bazlı rapor
- [x] Rule bazlı rapor
- [ ] Şirket standardı compliance raporu
- [ ] Önceki analizle karşılaştırmalı rapor
- [ ] AI Review’un rapora eklenmesi
- [ ] Fix önerilerinin rapora eklenmesi
- [ ] Report branding / şirket logosu

## 8. Rule Profiles

- [x] Custom Rule Profiles
- [x] Şirket standartlarına özel rule set
- [ ] Strict profile
- [ ] Legacy profile
- [ ] REFramework profile
- [ ] Modern UiPath profile
- [ ] Migration profile
- [x] Kullanıcı tarafından rule enable/disable
- [x] Rule severity özelleştirme
- [x] Rule weight özelleştirme
- [x] Rule max penalty özelleştirme
- [x] Şirket bazlı custom rule ekleme

## 9. Analysis History

- [x] Analysis history
- [x] Önceki analizle karşılaştırma
- [x] Skor değişim geçmişi
- [x] Yeni finding tespiti
- [x] Kapanan finding tespiti
- [x] Değişmeyen finding tespiti
- [x] Workflow bazlı değişim geçmişi
- [ ] Trend analizi

## 10. UiPath Integration

- [ ] UiPath Studio entegrasyonu
- [ ] UiPath Studio’dan RPA Dev Assistant açma
- [ ] UiPath Studio’da seçili workflow’u analiz etme
- [ ] Orchestrator entegrasyonu
- [ ] Orchestrator process analizi
- [ ] Queue analizi
- [ ] Asset analizi
- [ ] Robot / Machine konfigürasyon analizi
- [ ] Orchestrator API entegrasyonu
- [ ] Automation Suite desteği

## 11. Git & CI/CD

- [ ] GitHub entegrasyonu
- [ ] GitLab entegrasyonu
- [ ] Azure DevOps entegrasyonu
- [ ] Pull Request code review
- [ ] Commit bazlı analiz
- [ ] Branch karşılaştırması
- [ ] CI/CD quality gate
- [ ] Minimum score threshold
- [ ] Error/Critical finding varsa pipeline fail
- [ ] Review sonuçlarını PR comment olarak yazma

## 12. Company Internal Version

- [ ] Şirket içi merkezi kullanım
- [ ] Kullanıcı yönetimi
- [ ] Rol ve yetki yönetimi
- [ ] SSO
- [ ] Audit log — Kısmi: local apply/restore JSONL mutation audit log var; merkezi/kurumsal audit sistemi yok.
- [ ] Merkezi rule/profile yönetimi — Kısmi: local single-user custom rule/profile yönetimi, import/export ve default profile overlay tamamlandı; merkezi çok kullanıcılı governance yok.
- [ ] Merkezi analysis history
- [ ] Takım bazlı projeler
- [ ] Şirket dashboard’u
- [ ] Proje kalite trendleri
- [ ] Developer/team bazlı kalite metrikleri
- [ ] On-premise deployment

## 13. Enterprise / Customer Version

- [ ] Multi-tenant yapı
- [ ] Müşteri bazlı izolasyon
- [ ] Müşteri bazlı rule profile
- [ ] Müşteri bazlı branding
- [ ] Lisanslama
- [ ] Subscription modeli
- [ ] Kullanım kotası
- [ ] Enterprise SSO
- [ ] Audit / compliance
- [ ] Customer admin panel
- [ ] On-premise seçeneği
- [ ] SaaS seçeneği
- [ ] Private AI provider desteği

## 14. Product Platform

- [ ] Auto-update
- [ ] Telemetry / kullanım istatistikleri
- [ ] Crash reporting
- [ ] Plugin/modül sistemi
- [ ] Feature flag sistemi
- [ ] Configuration management — Kısmi: environment config, CORS origins ve built-in profile config var; genel config management yok.
- [ ] Desktop installer iyileştirmeleri — Kısmi: Tauri NSIS build script/config var; installer release/signing/upgrade akışı yok.
- [ ] Code signing
- [ ] Windows installer — Kısmi: NSIS Windows build hedefi ve script var; bu backlog incelemesinde installer artifact doğrulanmadı.
- [ ] macOS desteği
- [ ] Linux desteği

---

## Yeni Tespitler

> Codex repository incelemesi sırasında bu listede olmayan fakat eklenmesi gereken teknik borçları veya ürün özelliklerini buraya ekleyebilir.

- [ ] Repo kökünde istenen `PRODUCT_BACKLOG.md` yok; mevcut backlog dosyası `RPA_Dev_Assistant_Product_Backlog.md` adıyla duruyor.
- [ ] Frontend tek dosyada büyümüş durumda; dashboard/settings/workflows/fix/AI ekranları component dosyalarına ayrılmalı.
- [x] AI Review ve Ask Project cevap dili seçili locale’a göre garanti edilmiyor; prompt/request seviyesinde locale bağlandı ve TR/EN acceptance ile doğrulandı.
- [ ] RPA015 gerçek projede ambiguous finding üretiyor; default profile/severity veya AI-review yönlendirmesi ürün kararıyla netleştirilmeli.
- [ ] Findings ekranında ayrı bir RuleId/rule dropdown filtresi yok; RuleId araması şu anda global search ile yapılabiliyor.
- [x] Backend-generated Fix Suggestion title/description/steps/risks için locale-aware contract yok; backend fix suggestion metinleri TR/EN locale ile doğrulandı.
- [x] Frontend test runner ortamında testler arası localStorage dil durumu sızabiliyordu; testSetup.ts içerisine localStorage temizliği eklendi ve doğrulandı.

---

## İnceleme Özeti

**Son inceleme tarihi:** 2026-09-03  
**İnceleyen:** Jules
**Tamamlanan madde sayısı:** 75
**Kısmi madde sayısı:** 17
**Bekleyen madde sayısı:** 83

### Notlar

- `PRODUCT_BACKLOG.md` dosyası bulunmadığı için bu inceleme repository’deki mevcut backlog kaynağı olan `RPA_Dev_Assistant_Product_Backlog.md` üzerinde yapıldı.
- Tamamlandı işaretleri kod, test, endpoint, frontend entegrasyonu ve README kanıtlarıyla sınırlı tutuldu.
