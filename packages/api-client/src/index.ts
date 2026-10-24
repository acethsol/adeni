export { AdeniApiError, type AdeniApiClientOptions } from "./core/api-transport";
export type { DiscoverySearchParams } from "./discovery/discovery-api";
import { ApiTransport, type ApiClientContext } from "./core/api-transport";
import {
  type BookingResponse,
  type ServiceOffering,
  type QueueTicketResponse,
} from "@adeni/shared";
import { DiscoveryApi } from "./discovery/discovery-api";
import { BookingApi } from "./booking/booking-api";
import { AdminApi } from "./admin/admin-api";
import { FinanceApi } from "./finance/finance-api";
import { BusinessApi } from "./business/business-api";
import { SubscriptionsApi } from "./subscriptions/subscriptions-api";
import { QuotesApi } from "./quotes/quotes-api";
import { ReportingApi } from "./reporting/reporting-api";
import { QueueApi } from "./queue/queue-api";
import { CustomersApi } from "./customers/customers-api";
import { StaffApi } from "./staff/staff-api";
import { MessagingApi } from "./messaging/messaging-api";
import { CatalogApi } from "./catalog/catalog-api";

/** Compatibility facade: domain operations share the same mutable transport/session state. */
export class AdeniApiClient extends ApiTransport {
  private readonly context: ApiClientContext = {
    baseUrl: this.baseUrl,
    request: (path, init) => this.request(path, init),
  };
  private readonly discovery = new DiscoveryApi(this.context);
  private readonly booking = new BookingApi(this.context);
  private readonly admin = new AdminApi(this.context);
  private readonly finance = new FinanceApi(this.context);
  private readonly business = new BusinessApi(this.context);
  private readonly subscriptions = new SubscriptionsApi(this.context);
  private readonly quotes = new QuotesApi(this.context);
  private readonly reporting = new ReportingApi(this.context);
  private readonly queue = new QueueApi(this.context);
  private readonly customers = new CustomersApi(this.context);
  private readonly staff = new StaffApi(this.context);
  private readonly messaging = new MessagingApi(this.context);
  private readonly catalog = new CatalogApi(this.context);

  getCategories(...args: Parameters<DiscoveryApi["getCategories"]>): ReturnType<DiscoveryApi["getCategories"]> {
    return this.discovery.getCategories(...args);
  }

  getCategoryServiceTemplates(...args: Parameters<DiscoveryApi["getCategoryServiceTemplates"]>): ReturnType<DiscoveryApi["getCategoryServiceTemplates"]> {
    return this.discovery.getCategoryServiceTemplates(...args);
  }

  getMarkets(...args: Parameters<DiscoveryApi["getMarkets"]>): ReturnType<DiscoveryApi["getMarkets"]> {
    return this.discovery.getMarkets(...args);
  }

  searchDiscovery(...args: Parameters<DiscoveryApi["searchDiscovery"]>): ReturnType<DiscoveryApi["searchDiscovery"]> {
    return this.discovery.searchDiscovery(...args);
  }

  getBusinessProfile(...args: Parameters<DiscoveryApi["getBusinessProfile"]>): ReturnType<DiscoveryApi["getBusinessProfile"]> {
    return this.discovery.getBusinessProfile(...args);
  }

  getBusinessServiceCatalog(...args: Parameters<DiscoveryApi["getBusinessServiceCatalog"]>): ReturnType<DiscoveryApi["getBusinessServiceCatalog"]> {
    return this.discovery.getBusinessServiceCatalog(...args);
  }

  async getBusinessServices(slug: string): Promise<ServiceOffering[]> {
    const catalog = await this.getBusinessServiceCatalog(slug);
    return catalog.items;
  }

  getBusinessStaff(...args: Parameters<DiscoveryApi["getBusinessStaff"]>): ReturnType<DiscoveryApi["getBusinessStaff"]> {
    return this.discovery.getBusinessStaff(...args);
  }

  getBusinessSlots(...args: Parameters<DiscoveryApi["getBusinessSlots"]>): ReturnType<DiscoveryApi["getBusinessSlots"]> {
    return this.discovery.getBusinessSlots(...args);
  }

  createBooking(...args: Parameters<BookingApi["createBooking"]>): ReturnType<BookingApi["createBooking"]> {
    return this.booking.createBooking(...args);
  }

  getMyBookings(...args: Parameters<BookingApi["getMyBookings"]>): ReturnType<BookingApi["getMyBookings"]> {
    return this.booking.getMyBookings(...args);
  }

  cancelMyBooking(...args: Parameters<BookingApi["cancelMyBooking"]>): ReturnType<BookingApi["cancelMyBooking"]> {
    return this.booking.cancelMyBooking(...args);
  }

  createBookingReview(...args: Parameters<BookingApi["createBookingReview"]>): ReturnType<BookingApi["createBookingReview"]> {
    return this.booking.createBookingReview(...args);
  }

  getBusinessReviews(...args: Parameters<DiscoveryApi["getBusinessReviews"]>): ReturnType<DiscoveryApi["getBusinessReviews"]> {
    return this.discovery.getBusinessReviews(...args);
  }

  listAdminReviews(...args: Parameters<AdminApi["listAdminReviews"]>): ReturnType<AdminApi["listAdminReviews"]> {
    return this.admin.listAdminReviews(...args);
  }

  hideAdminReview(...args: Parameters<AdminApi["hideAdminReview"]>): ReturnType<AdminApi["hideAdminReview"]> {
    return this.admin.hideAdminReview(...args);
  }

  listAdminPendingBadges(...args: Parameters<AdminApi["listAdminPendingBadges"]>): ReturnType<AdminApi["listAdminPendingBadges"]> {
    return this.admin.listAdminPendingBadges(...args);
  }

  grantAdminVerificationBadge(...args: Parameters<AdminApi["grantAdminVerificationBadge"]>): ReturnType<AdminApi["grantAdminVerificationBadge"]> {
    return this.admin.grantAdminVerificationBadge(...args);
  }

  denyAdminVerificationBadge(...args: Parameters<AdminApi["denyAdminVerificationBadge"]>): ReturnType<AdminApi["denyAdminVerificationBadge"]> {
    return this.admin.denyAdminVerificationBadge(...args);
  }

  listTenantBookings(...args: Parameters<BookingApi["listTenantBookings"]>): ReturnType<BookingApi["listTenantBookings"]> {
    return this.booking.listTenantBookings(...args);
  }

  /** @deprecated Use listTenantBookings — returns first page of pending tab only. */
  async getTenantBookings(): Promise<BookingResponse[]> {
    return (await this.listTenantBookings({ tab: "pending", page: 1, pageSize: 100 })).items;
  }

  acceptTenantBooking(...args: Parameters<BookingApi["acceptTenantBooking"]>): ReturnType<BookingApi["acceptTenantBooking"]> {
    return this.booking.acceptTenantBooking(...args);
  }

  rejectTenantBooking(...args: Parameters<BookingApi["rejectTenantBooking"]>): ReturnType<BookingApi["rejectTenantBooking"]> {
    return this.booking.rejectTenantBooking(...args);
  }

  checkInTenantBooking(...args: Parameters<BookingApi["checkInTenantBooking"]>): ReturnType<BookingApi["checkInTenantBooking"]> {
    return this.booking.checkInTenantBooking(...args);
  }

  completeTenantBooking(...args: Parameters<BookingApi["completeTenantBooking"]>): ReturnType<BookingApi["completeTenantBooking"]> {
    return this.booking.completeTenantBooking(...args);
  }

  markTenantBookingPaid(...args: Parameters<BookingApi["markTenantBookingPaid"]>): ReturnType<BookingApi["markTenantBookingPaid"]> {
    return this.booking.markTenantBookingPaid(...args);
  }

  collectTenantBookingBalance(...args: Parameters<BookingApi["collectTenantBookingBalance"]>): ReturnType<BookingApi["collectTenantBookingBalance"]> {
    return this.booking.collectTenantBookingBalance(...args);
  }

  getPaymentReceipt(...args: Parameters<FinanceApi["getPaymentReceipt"]>): ReturnType<FinanceApi["getPaymentReceipt"]> {
    return this.finance.getPaymentReceipt(...args);
  }

  listBillingDocuments(...args: Parameters<FinanceApi["listBillingDocuments"]>): ReturnType<FinanceApi["listBillingDocuments"]> {
    return this.finance.listBillingDocuments(...args);
  }

  getBillingDocument(...args: Parameters<FinanceApi["getBillingDocument"]>): ReturnType<FinanceApi["getBillingDocument"]> {
    return this.finance.getBillingDocument(...args);
  }

  getBillingDocumentPdfUrl(...args: Parameters<FinanceApi["getBillingDocumentPdfUrl"]>): ReturnType<FinanceApi["getBillingDocumentPdfUrl"]> {
    return this.finance.getBillingDocumentPdfUrl(...args);
  }

  downloadBillingDocumentPdf(...args: Parameters<FinanceApi["downloadBillingDocumentPdf"]>): ReturnType<FinanceApi["downloadBillingDocumentPdf"]> {
    return this.finance.downloadBillingDocumentPdf(...args);
  }

  emailBillingDocument(...args: Parameters<FinanceApi["emailBillingDocument"]>): ReturnType<FinanceApi["emailBillingDocument"]> {
    return this.finance.emailBillingDocument(...args);
  }

  createInvoice(...args: Parameters<FinanceApi["createInvoice"]>): ReturnType<FinanceApi["createInvoice"]> {
    return this.finance.createInvoice(...args);
  }

  getPublicBillingDocument(...args: Parameters<FinanceApi["getPublicBillingDocument"]>): ReturnType<FinanceApi["getPublicBillingDocument"]> {
    return this.finance.getPublicBillingDocument(...args);
  }

  getTenantProfile(...args: Parameters<BusinessApi["getTenantProfile"]>): ReturnType<BusinessApi["getTenantProfile"]> {
    return this.business.getTenantProfile(...args);
  }

  getTenantEntitlements(...args: Parameters<SubscriptionsApi["getTenantEntitlements"]>): ReturnType<SubscriptionsApi["getTenantEntitlements"]> {
    return this.subscriptions.getTenantEntitlements(...args);
  }

  getBusinessContext(...args: Parameters<BusinessApi["getBusinessContext"]>): ReturnType<BusinessApi["getBusinessContext"]> {
    return this.business.getBusinessContext(...args);
  }

  registerBusiness(...args: Parameters<BusinessApi["registerBusiness"]>): ReturnType<BusinessApi["registerBusiness"]> {
    return this.business.registerBusiness(...args);
  }

  updateTenantProfile(...args: Parameters<BusinessApi["updateTenantProfile"]>): ReturnType<BusinessApi["updateTenantProfile"]> {
    return this.business.updateTenantProfile(...args);
  }

  updateTenantSettings(...args: Parameters<BusinessApi["updateTenantSettings"]>): ReturnType<BusinessApi["updateTenantSettings"]> {
    return this.business.updateTenantSettings(...args);
  }

  updateTenantPublicPage(...args: Parameters<BusinessApi["updateTenantPublicPage"]>): ReturnType<BusinessApi["updateTenantPublicPage"]> {
    return this.business.updateTenantPublicPage(...args);
  }

  updateTenantPolicies(...args: Parameters<BusinessApi["updateTenantPolicies"]>): ReturnType<BusinessApi["updateTenantPolicies"]> {
    return this.business.updateTenantPolicies(...args);
  }

  createQuotePhotoUploadUrl(...args: Parameters<QuotesApi["createQuotePhotoUploadUrl"]>): ReturnType<QuotesApi["createQuotePhotoUploadUrl"]> {
    return this.quotes.createQuotePhotoUploadUrl(...args);
  }

  listTenantVerificationBadges(...args: Parameters<BusinessApi["listTenantVerificationBadges"]>): ReturnType<BusinessApi["listTenantVerificationBadges"]> {
    return this.business.listTenantVerificationBadges(...args);
  }

  createQuoteRequest(...args: Parameters<DiscoveryApi["createQuoteRequest"]>): ReturnType<DiscoveryApi["createQuoteRequest"]> {
    return this.discovery.createQuoteRequest(...args);
  }

  listCustomerQuotes(...args: Parameters<QuotesApi["listCustomerQuotes"]>): ReturnType<QuotesApi["listCustomerQuotes"]> {
    return this.quotes.listCustomerQuotes(...args);
  }

  acceptQuote(...args: Parameters<QuotesApi["acceptQuote"]>): ReturnType<QuotesApi["acceptQuote"]> {
    return this.quotes.acceptQuote(...args);
  }

  declineQuote(...args: Parameters<QuotesApi["declineQuote"]>): ReturnType<QuotesApi["declineQuote"]> {
    return this.quotes.declineQuote(...args);
  }

  listTenantQuotes(...args: Parameters<QuotesApi["listTenantQuotes"]>): ReturnType<QuotesApi["listTenantQuotes"]> {
    return this.quotes.listTenantQuotes(...args);
  }

  downloadTenantQuotePdf(...args: Parameters<ReportingApi["downloadTenantQuotePdf"]>): ReturnType<ReportingApi["downloadTenantQuotePdf"]> {
    return this.reporting.downloadTenantQuotePdf(...args);
  }

  downloadTenantCustomerStatementPdf(...args: Parameters<ReportingApi["downloadTenantCustomerStatementPdf"]>): ReturnType<ReportingApi["downloadTenantCustomerStatementPdf"]> {
    return this.reporting.downloadTenantCustomerStatementPdf(...args);
  }

  submitQuoteOffer(...args: Parameters<QuotesApi["submitQuoteOffer"]>): ReturnType<QuotesApi["submitQuoteOffer"]> {
    return this.quotes.submitQuoteOffer(...args);
  }

  listTenantReviews(...args: Parameters<BusinessApi["listTenantReviews"]>): ReturnType<BusinessApi["listTenantReviews"]> {
    return this.business.listTenantReviews(...args);
  }

  replyToReview(...args: Parameters<BusinessApi["replyToReview"]>): ReturnType<BusinessApi["replyToReview"]> {
    return this.business.replyToReview(...args);
  }

  requestVerificationBadge(...args: Parameters<BusinessApi["requestVerificationBadge"]>): ReturnType<BusinessApi["requestVerificationBadge"]> {
    return this.business.requestVerificationBadge(...args);
  }

  joinWaitlist(...args: Parameters<BookingApi["joinWaitlist"]>): ReturnType<BookingApi["joinWaitlist"]> {
    return this.booking.joinWaitlist(...args);
  }

  joinPublicQueue(...args: Parameters<QueueApi["joinPublicQueue"]>): ReturnType<QueueApi["joinPublicQueue"]> {
    return this.queue.joinPublicQueue(...args);
  }

  getPublicQueueTicket(...args: Parameters<QueueApi["getPublicQueueTicket"]>): ReturnType<QueueApi["getPublicQueueTicket"]> {
    return this.queue.getPublicQueueTicket(...args);
  }

  getTenantQueueFloor(...args: Parameters<QueueApi["getTenantQueueFloor"]>): ReturnType<QueueApi["getTenantQueueFloor"]> {
    return this.queue.getTenantQueueFloor(...args);
  }

  /** @deprecated Use getTenantQueueFloor */
  async getTenantQueue(): Promise<QueueTicketResponse[]> {
    const floor = await this.getTenantQueueFloor();
    return floor.items;
  }

  updateWalkInSettings(...args: Parameters<QueueApi["updateWalkInSettings"]>): ReturnType<QueueApi["updateWalkInSettings"]> {
    return this.queue.updateWalkInSettings(...args);
  }

  openWalkInBlock(...args: Parameters<QueueApi["openWalkInBlock"]>): ReturnType<QueueApi["openWalkInBlock"]> {
    return this.queue.openWalkInBlock(...args);
  }

  addTenantQueueTicket(...args: Parameters<QueueApi["addTenantQueueTicket"]>): ReturnType<QueueApi["addTenantQueueTicket"]> {
    return this.queue.addTenantQueueTicket(...args);
  }

  callTenantQueueTicket(...args: Parameters<QueueApi["callTenantQueueTicket"]>): ReturnType<QueueApi["callTenantQueueTicket"]> {
    return this.queue.callTenantQueueTicket(...args);
  }

  startTenantQueueTicket(...args: Parameters<QueueApi["startTenantQueueTicket"]>): ReturnType<QueueApi["startTenantQueueTicket"]> {
    return this.queue.startTenantQueueTicket(...args);
  }

  completeTenantQueueTicket(...args: Parameters<QueueApi["completeTenantQueueTicket"]>): ReturnType<QueueApi["completeTenantQueueTicket"]> {
    return this.queue.completeTenantQueueTicket(...args);
  }

  noShowTenantQueueTicket(...args: Parameters<QueueApi["noShowTenantQueueTicket"]>): ReturnType<QueueApi["noShowTenantQueueTicket"]> {
    return this.queue.noShowTenantQueueTicket(...args);
  }

  cancelTenantQueueTicket(...args: Parameters<QueueApi["cancelTenantQueueTicket"]>): ReturnType<QueueApi["cancelTenantQueueTicket"]> {
    return this.queue.cancelTenantQueueTicket(...args);
  }

  listTenantCustomers(...args: Parameters<CustomersApi["listTenantCustomers"]>): ReturnType<CustomersApi["listTenantCustomers"]> {
    return this.customers.listTenantCustomers(...args);
  }

  getTenantCustomer(...args: Parameters<CustomersApi["getTenantCustomer"]>): ReturnType<CustomersApi["getTenantCustomer"]> {
    return this.customers.getTenantCustomer(...args);
  }

  getTenantKioskStatus(...args: Parameters<QueueApi["getTenantKioskStatus"]>): ReturnType<QueueApi["getTenantKioskStatus"]> {
    return this.queue.getTenantKioskStatus(...args);
  }

  setTenantKioskPin(...args: Parameters<QueueApi["setTenantKioskPin"]>): ReturnType<QueueApi["setTenantKioskPin"]> {
    return this.queue.setTenantKioskPin(...args);
  }

  verifyTenantKioskPin(...args: Parameters<QueueApi["verifyTenantKioskPin"]>): ReturnType<QueueApi["verifyTenantKioskPin"]> {
    return this.queue.verifyTenantKioskPin(...args);
  }

  initializePayment(...args: Parameters<FinanceApi["initializePayment"]>): ReturnType<FinanceApi["initializePayment"]> {
    return this.finance.initializePayment(...args);
  }

  getPayment(...args: Parameters<FinanceApi["getPayment"]>): ReturnType<FinanceApi["getPayment"]> {
    return this.finance.getPayment(...args);
  }

  createPaymentLink(...args: Parameters<FinanceApi["createPaymentLink"]>): ReturnType<FinanceApi["createPaymentLink"]> {
    return this.finance.createPaymentLink(...args);
  }

  listPaymentLedger(...args: Parameters<FinanceApi["listPaymentLedger"]>): ReturnType<FinanceApi["listPaymentLedger"]> {
    return this.finance.listPaymentLedger(...args);
  }

  refundPayment(...args: Parameters<FinanceApi["refundPayment"]>): ReturnType<FinanceApi["refundPayment"]> {
    return this.finance.refundPayment(...args);
  }

  downloadCompensationExport(...args: Parameters<ReportingApi["downloadCompensationExport"]>): ReturnType<ReportingApi["downloadCompensationExport"]> {
    return this.reporting.downloadCompensationExport(...args);
  }

  downloadAdminReportExport(...args: Parameters<AdminApi["downloadAdminReportExport"]>): ReturnType<AdminApi["downloadAdminReportExport"]> {
    return this.admin.downloadAdminReportExport(...args);
  }

  downloadTenantReportExport(...args: Parameters<ReportingApi["downloadTenantReportExport"]>): ReturnType<ReportingApi["downloadTenantReportExport"]> {
    return this.reporting.downloadTenantReportExport(...args);
  }

  confirmStubPayment(...args: Parameters<FinanceApi["confirmStubPayment"]>): ReturnType<FinanceApi["confirmStubPayment"]> {
    return this.finance.confirmStubPayment(...args);
  }

  createCoverUploadUrl(...args: Parameters<BusinessApi["createCoverUploadUrl"]>): ReturnType<BusinessApi["createCoverUploadUrl"]> {
    return this.business.createCoverUploadUrl(...args);
  }

  createGalleryUploadUrl(...args: Parameters<BusinessApi["createGalleryUploadUrl"]>): ReturnType<BusinessApi["createGalleryUploadUrl"]> {
    return this.business.createGalleryUploadUrl(...args);
  }

  createStaffAvatarUploadUrl(...args: Parameters<StaffApi["createStaffAvatarUploadUrl"]>): ReturnType<StaffApi["createStaffAvatarUploadUrl"]> {
    return this.staff.createStaffAvatarUploadUrl(...args);
  }

  createMessageAttachmentUploadUrl(...args: Parameters<MessagingApi["createMessageAttachmentUploadUrl"]>): ReturnType<MessagingApi["createMessageAttachmentUploadUrl"]> {
    return this.messaging.createMessageAttachmentUploadUrl(...args);
  }

  updateTenantStaffAvatar(...args: Parameters<StaffApi["updateTenantStaffAvatar"]>): ReturnType<StaffApi["updateTenantStaffAvatar"]> {
    return this.staff.updateTenantStaffAvatar(...args);
  }

  updateTenantCoverImage(...args: Parameters<BusinessApi["updateTenantCoverImage"]>): ReturnType<BusinessApi["updateTenantCoverImage"]> {
    return this.business.updateTenantCoverImage(...args);
  }

  addTenantGalleryImage(...args: Parameters<BusinessApi["addTenantGalleryImage"]>): ReturnType<BusinessApi["addTenantGalleryImage"]> {
    return this.business.addTenantGalleryImage(...args);
  }

  removeTenantGalleryImage(...args: Parameters<BusinessApi["removeTenantGalleryImage"]>): ReturnType<BusinessApi["removeTenantGalleryImage"]> {
    return this.business.removeTenantGalleryImage(...args);
  }

  getTenantServiceCatalog(...args: Parameters<CatalogApi["getTenantServiceCatalog"]>): ReturnType<CatalogApi["getTenantServiceCatalog"]> {
    return this.catalog.getTenantServiceCatalog(...args);
  }

  async getTenantServices(): Promise<ServiceOffering[]> {
    const catalog = await this.getTenantServiceCatalog();
    return catalog.items;
  }

  listTenantServiceMenuGroups(...args: Parameters<CatalogApi["listTenantServiceMenuGroups"]>): ReturnType<CatalogApi["listTenantServiceMenuGroups"]> {
    return this.catalog.listTenantServiceMenuGroups(...args);
  }

  createTenantServiceMenuGroup(...args: Parameters<CatalogApi["createTenantServiceMenuGroup"]>): ReturnType<CatalogApi["createTenantServiceMenuGroup"]> {
    return this.catalog.createTenantServiceMenuGroup(...args);
  }

  updateTenantServiceMenuGroup(...args: Parameters<CatalogApi["updateTenantServiceMenuGroup"]>): ReturnType<CatalogApi["updateTenantServiceMenuGroup"]> {
    return this.catalog.updateTenantServiceMenuGroup(...args);
  }

  deleteTenantServiceMenuGroup(...args: Parameters<CatalogApi["deleteTenantServiceMenuGroup"]>): ReturnType<CatalogApi["deleteTenantServiceMenuGroup"]> {
    return this.catalog.deleteTenantServiceMenuGroup(...args);
  }

  createTenantService(...args: Parameters<CatalogApi["createTenantService"]>): ReturnType<CatalogApi["createTenantService"]> {
    return this.catalog.createTenantService(...args);
  }

  updateTenantService(...args: Parameters<CatalogApi["updateTenantService"]>): ReturnType<CatalogApi["updateTenantService"]> {
    return this.catalog.updateTenantService(...args);
  }

  deactivateTenantService(...args: Parameters<CatalogApi["deactivateTenantService"]>): ReturnType<CatalogApi["deactivateTenantService"]> {
    return this.catalog.deactivateTenantService(...args);
  }

  listTenantStaff(...args: Parameters<StaffApi["listTenantStaff"]>): ReturnType<StaffApi["listTenantStaff"]> {
    return this.staff.listTenantStaff(...args);
  }

  createTenantStaff(...args: Parameters<StaffApi["createTenantStaff"]>): ReturnType<StaffApi["createTenantStaff"]> {
    return this.staff.createTenantStaff(...args);
  }

  updateTenantStaff(...args: Parameters<StaffApi["updateTenantStaff"]>): ReturnType<StaffApi["updateTenantStaff"]> {
    return this.staff.updateTenantStaff(...args);
  }

  deactivateTenantStaff(...args: Parameters<StaffApi["deactivateTenantStaff"]>): ReturnType<StaffApi["deactivateTenantStaff"]> {
    return this.staff.deactivateTenantStaff(...args);
  }

  inviteTenantStaff(...args: Parameters<StaffApi["inviteTenantStaff"]>): ReturnType<StaffApi["inviteTenantStaff"]> {
    return this.staff.inviteTenantStaff(...args);
  }

  inviteTenantAccess(...args: Parameters<StaffApi["inviteTenantAccess"]>): ReturnType<StaffApi["inviteTenantAccess"]> {
    return this.staff.inviteTenantAccess(...args);
  }

  listTenantAccessInvites(...args: Parameters<StaffApi["listTenantAccessInvites"]>): ReturnType<StaffApi["listTenantAccessInvites"]> {
    return this.staff.listTenantAccessInvites(...args);
  }

  resendTenantAccessInvite(...args: Parameters<StaffApi["resendTenantAccessInvite"]>): ReturnType<StaffApi["resendTenantAccessInvite"]> {
    return this.staff.resendTenantAccessInvite(...args);
  }

  revokeTenantAccessInvite(...args: Parameters<StaffApi["revokeTenantAccessInvite"]>): ReturnType<StaffApi["revokeTenantAccessInvite"]> {
    return this.staff.revokeTenantAccessInvite(...args);
  }

  listTenantAccessUsers(...args: Parameters<StaffApi["listTenantAccessUsers"]>): ReturnType<StaffApi["listTenantAccessUsers"]> {
    return this.staff.listTenantAccessUsers(...args);
  }

  updateTenantAccessUser(...args: Parameters<StaffApi["updateTenantAccessUser"]>): ReturnType<StaffApi["updateTenantAccessUser"]> {
    return this.staff.updateTenantAccessUser(...args);
  }

  revokeTenantAccessUser(...args: Parameters<StaffApi["revokeTenantAccessUser"]>): ReturnType<StaffApi["revokeTenantAccessUser"]> {
    return this.staff.revokeTenantAccessUser(...args);
  }

  acceptStaffInvite(...args: Parameters<StaffApi["acceptStaffInvite"]>): ReturnType<StaffApi["acceptStaffInvite"]> {
    return this.staff.acceptStaffInvite(...args);
  }

  replaceTenantStaffServices(...args: Parameters<StaffApi["replaceTenantStaffServices"]>): ReturnType<StaffApi["replaceTenantStaffServices"]> {
    return this.staff.replaceTenantStaffServices(...args);
  }

  getTenantStaffHours(...args: Parameters<StaffApi["getTenantStaffHours"]>): ReturnType<StaffApi["getTenantStaffHours"]> {
    return this.staff.getTenantStaffHours(...args);
  }

  replaceTenantStaffHours(...args: Parameters<StaffApi["replaceTenantStaffHours"]>): ReturnType<StaffApi["replaceTenantStaffHours"]> {
    return this.staff.replaceTenantStaffHours(...args);
  }

  listTenantStaffLeave(...args: Parameters<StaffApi["listTenantStaffLeave"]>): ReturnType<StaffApi["listTenantStaffLeave"]> {
    return this.staff.listTenantStaffLeave(...args);
  }

  createTenantStaffLeave(...args: Parameters<StaffApi["createTenantStaffLeave"]>): ReturnType<StaffApi["createTenantStaffLeave"]> {
    return this.staff.createTenantStaffLeave(...args);
  }

  deleteTenantStaffLeave(...args: Parameters<StaffApi["deleteTenantStaffLeave"]>): ReturnType<StaffApi["deleteTenantStaffLeave"]> {
    return this.staff.deleteTenantStaffLeave(...args);
  }

  getTenantStaffCalendar(...args: Parameters<StaffApi["getTenantStaffCalendar"]>): ReturnType<StaffApi["getTenantStaffCalendar"]> {
    return this.staff.getTenantStaffCalendar(...args);
  }

  getTenantTeamCalendar(...args: Parameters<StaffApi["getTenantTeamCalendar"]>): ReturnType<StaffApi["getTenantTeamCalendar"]> {
    return this.staff.getTenantTeamCalendar(...args);
  }

  getTenantAvailability(...args: Parameters<BookingApi["getTenantAvailability"]>): ReturnType<BookingApi["getTenantAvailability"]> {
    return this.booking.getTenantAvailability(...args);
  }

  replaceTenantAvailability(...args: Parameters<BookingApi["replaceTenantAvailability"]>): ReturnType<BookingApi["replaceTenantAvailability"]> {
    return this.booking.replaceTenantAvailability(...args);
  }

  getTenantClosedDates(...args: Parameters<BookingApi["getTenantClosedDates"]>): ReturnType<BookingApi["getTenantClosedDates"]> {
    return this.booking.getTenantClosedDates(...args);
  }

  createTenantClosedDate(...args: Parameters<BookingApi["createTenantClosedDate"]>): ReturnType<BookingApi["createTenantClosedDate"]> {
    return this.booking.createTenantClosedDate(...args);
  }

  deleteTenantClosedDate(...args: Parameters<BookingApi["deleteTenantClosedDate"]>): ReturnType<BookingApi["deleteTenantClosedDate"]> {
    return this.booking.deleteTenantClosedDate(...args);
  }

  submitTenantVerification(...args: Parameters<BusinessApi["submitTenantVerification"]>): ReturnType<BusinessApi["submitTenantVerification"]> {
    return this.business.submitTenantVerification(...args);
  }

  getTenantLocations(...args: Parameters<BusinessApi["getTenantLocations"]>): ReturnType<BusinessApi["getTenantLocations"]> {
    return this.business.getTenantLocations(...args);
  }

  addTenantLocation(...args: Parameters<BusinessApi["addTenantLocation"]>): ReturnType<BusinessApi["addTenantLocation"]> {
    return this.business.addTenantLocation(...args);
  }

  updateTenantLocation(...args: Parameters<BusinessApi["updateTenantLocation"]>): ReturnType<BusinessApi["updateTenantLocation"]> {
    return this.business.updateTenantLocation(...args);
  }

  deactivateTenantLocation(...args: Parameters<BusinessApi["deactivateTenantLocation"]>): ReturnType<BusinessApi["deactivateTenantLocation"]> {
    return this.business.deactivateTenantLocation(...args);
  }

  getTenantSubscriptionUsage(...args: Parameters<SubscriptionsApi["getTenantSubscriptionUsage"]>): ReturnType<SubscriptionsApi["getTenantSubscriptionUsage"]> {
    return this.subscriptions.getTenantSubscriptionUsage(...args);
  }

  getTenantDashboardSummary(...args: Parameters<ReportingApi["getTenantDashboardSummary"]>): ReturnType<ReportingApi["getTenantDashboardSummary"]> {
    return this.reporting.getTenantDashboardSummary(...args);
  }

  getTenantDashboardSeries(...args: Parameters<ReportingApi["getTenantDashboardSeries"]>): ReturnType<ReportingApi["getTenantDashboardSeries"]> {
    return this.reporting.getTenantDashboardSeries(...args);
  }

  getTenantDashboardBreakdown(...args: Parameters<ReportingApi["getTenantDashboardBreakdown"]>): ReturnType<ReportingApi["getTenantDashboardBreakdown"]> {
    return this.reporting.getTenantDashboardBreakdown(...args);
  }

  getTenantDashboardLayout(...args: Parameters<ReportingApi["getTenantDashboardLayout"]>): ReturnType<ReportingApi["getTenantDashboardLayout"]> {
    return this.reporting.getTenantDashboardLayout(...args);
  }

  putTenantDashboardLayout(...args: Parameters<ReportingApi["putTenantDashboardLayout"]>): ReturnType<ReportingApi["putTenantDashboardLayout"]> {
    return this.reporting.putTenantDashboardLayout(...args);
  }

  getTenantBillingHistory(...args: Parameters<FinanceApi["getTenantBillingHistory"]>): ReturnType<FinanceApi["getTenantBillingHistory"]> {
    return this.finance.getTenantBillingHistory(...args);
  }

  createSubscriptionCheckout(...args: Parameters<SubscriptionsApi["createSubscriptionCheckout"]>): ReturnType<SubscriptionsApi["createSubscriptionCheckout"]> {
    return this.subscriptions.createSubscriptionCheckout(...args);
  }

  completeStubSubscriptionCheckout(...args: Parameters<SubscriptionsApi["completeStubSubscriptionCheckout"]>): ReturnType<SubscriptionsApi["completeStubSubscriptionCheckout"]> {
    return this.subscriptions.completeStubSubscriptionCheckout(...args);
  }

  getMe(...args: Parameters<BusinessApi["getMe"]>): ReturnType<BusinessApi["getMe"]> {
    return this.business.getMe(...args);
  }

  listPendingBusinesses(...args: Parameters<AdminApi["listPendingBusinesses"]>): ReturnType<AdminApi["listPendingBusinesses"]> {
    return this.admin.listPendingBusinesses(...args);
  }

  approvePendingBusiness(...args: Parameters<AdminApi["approvePendingBusiness"]>): ReturnType<AdminApi["approvePendingBusiness"]> {
    return this.admin.approvePendingBusiness(...args);
  }

  rejectPendingBusiness(...args: Parameters<AdminApi["rejectPendingBusiness"]>): ReturnType<AdminApi["rejectPendingBusiness"]> {
    return this.admin.rejectPendingBusiness(...args);
  }

  listAdminBusinesses(...args: Parameters<AdminApi["listAdminBusinesses"]>): ReturnType<AdminApi["listAdminBusinesses"]> {
    return this.admin.listAdminBusinesses(...args);
  }

  getAdminBusinessDirectoryStats(...args: Parameters<AdminApi["getAdminBusinessDirectoryStats"]>): ReturnType<AdminApi["getAdminBusinessDirectoryStats"]> {
    return this.admin.getAdminBusinessDirectoryStats(...args);
  }

  getAdminSubscriptionMetrics(...args: Parameters<AdminApi["getAdminSubscriptionMetrics"]>): ReturnType<AdminApi["getAdminSubscriptionMetrics"]> {
    return this.admin.getAdminSubscriptionMetrics(...args);
  }

  getAdminPlatformSubscriptionMrr(...args: Parameters<AdminApi["getAdminPlatformSubscriptionMrr"]>): ReturnType<AdminApi["getAdminPlatformSubscriptionMrr"]> {
    return this.admin.getAdminPlatformSubscriptionMrr(...args);
  }

  getAdminReportingGmv(...args: Parameters<AdminApi["getAdminReportingGmv"]>): ReturnType<AdminApi["getAdminReportingGmv"]> {
    return this.admin.getAdminReportingGmv(...args);
  }

  getAdminRevenueMetrics(...args: Parameters<AdminApi["getAdminRevenueMetrics"]>): ReturnType<AdminApi["getAdminRevenueMetrics"]> {
    return this.admin.getAdminRevenueMetrics(...args);
  }

  searchAdminTransactions(...args: Parameters<AdminApi["searchAdminTransactions"]>): ReturnType<AdminApi["searchAdminTransactions"]> {
    return this.admin.searchAdminTransactions(...args);
  }

  listAdminAuditLogs(...args: Parameters<AdminApi["listAdminAuditLogs"]>): ReturnType<AdminApi["listAdminAuditLogs"]> {
    return this.admin.listAdminAuditLogs(...args);
  }

  getAdminBusiness(...args: Parameters<AdminApi["getAdminBusiness"]>): ReturnType<AdminApi["getAdminBusiness"]> {
    return this.admin.getAdminBusiness(...args);
  }

  listAdminTenantCommercialAudit(...args: Parameters<AdminApi["listAdminTenantCommercialAudit"]>): ReturnType<AdminApi["listAdminTenantCommercialAudit"]> {
    return this.admin.listAdminTenantCommercialAudit(...args);
  }

  getAdminTenantCommercialTerms(...args: Parameters<AdminApi["getAdminTenantCommercialTerms"]>): ReturnType<AdminApi["getAdminTenantCommercialTerms"]> {
    return this.admin.getAdminTenantCommercialTerms(...args);
  }

  setAdminTenantCommercialTerms(...args: Parameters<AdminApi["setAdminTenantCommercialTerms"]>): ReturnType<AdminApi["setAdminTenantCommercialTerms"]> {
    return this.admin.setAdminTenantCommercialTerms(...args);
  }

  getPublicPricing(...args: Parameters<SubscriptionsApi["getPublicPricing"]>): ReturnType<SubscriptionsApi["getPublicPricing"]> {
    return this.subscriptions.getPublicPricing(...args);
  }

  listAdminPricingCatalogVersions(...args: Parameters<AdminApi["listAdminPricingCatalogVersions"]>): ReturnType<AdminApi["listAdminPricingCatalogVersions"]> {
    return this.admin.listAdminPricingCatalogVersions(...args);
  }

  getAdminPricingCatalogVersion(...args: Parameters<AdminApi["getAdminPricingCatalogVersion"]>): ReturnType<AdminApi["getAdminPricingCatalogVersion"]> {
    return this.admin.getAdminPricingCatalogVersion(...args);
  }

  createAdminPricingCatalogVersion(...args: Parameters<AdminApi["createAdminPricingCatalogVersion"]>): ReturnType<AdminApi["createAdminPricingCatalogVersion"]> {
    return this.admin.createAdminPricingCatalogVersion(...args);
  }

  upsertAdminPricingCatalogEntries(...args: Parameters<AdminApi["upsertAdminPricingCatalogEntries"]>): ReturnType<AdminApi["upsertAdminPricingCatalogEntries"]> {
    return this.admin.upsertAdminPricingCatalogEntries(...args);
  }

  activateAdminPricingCatalogVersion(...args: Parameters<AdminApi["activateAdminPricingCatalogVersion"]>): ReturnType<AdminApi["activateAdminPricingCatalogVersion"]> {
    return this.admin.activateAdminPricingCatalogVersion(...args);
  }

  listAdminTenantSubscriptionCharges(...args: Parameters<AdminApi["listAdminTenantSubscriptionCharges"]>): ReturnType<AdminApi["listAdminTenantSubscriptionCharges"]> {
    return this.admin.listAdminTenantSubscriptionCharges(...args);
  }

  searchAdminCustomers(...args: Parameters<AdminApi["searchAdminCustomers"]>): ReturnType<AdminApi["searchAdminCustomers"]> {
    return this.admin.searchAdminCustomers(...args);
  }

  exportAdminCustomer(...args: Parameters<AdminApi["exportAdminCustomer"]>): ReturnType<AdminApi["exportAdminCustomer"]> {
    return this.admin.exportAdminCustomer(...args);
  }

  initiateAdminCustomerDelete(...args: Parameters<AdminApi["initiateAdminCustomerDelete"]>): ReturnType<AdminApi["initiateAdminCustomerDelete"]> {
    return this.admin.initiateAdminCustomerDelete(...args);
  }

  getAdminMarketCatalogStats(...args: Parameters<AdminApi["getAdminMarketCatalogStats"]>): ReturnType<AdminApi["getAdminMarketCatalogStats"]> {
    return this.admin.getAdminMarketCatalogStats(...args);
  }

  listAdminMarkets(...args: Parameters<AdminApi["listAdminMarkets"]>): ReturnType<AdminApi["listAdminMarkets"]> {
    return this.admin.listAdminMarkets(...args);
  }

  createAdminMarket(...args: Parameters<AdminApi["createAdminMarket"]>): ReturnType<AdminApi["createAdminMarket"]> {
    return this.admin.createAdminMarket(...args);
  }

  updateAdminMarket(...args: Parameters<AdminApi["updateAdminMarket"]>): ReturnType<AdminApi["updateAdminMarket"]> {
    return this.admin.updateAdminMarket(...args);
  }

  setAdminMarketLive(...args: Parameters<AdminApi["setAdminMarketLive"]>): ReturnType<AdminApi["setAdminMarketLive"]> {
    return this.admin.setAdminMarketLive(...args);
  }

  createConversation(...args: Parameters<MessagingApi["createConversation"]>): ReturnType<MessagingApi["createConversation"]> {
    return this.messaging.createConversation(...args);
  }

  listCustomerConversations(...args: Parameters<MessagingApi["listCustomerConversations"]>): ReturnType<MessagingApi["listCustomerConversations"]> {
    return this.messaging.listCustomerConversations(...args);
  }

  getCustomerConversation(...args: Parameters<MessagingApi["getCustomerConversation"]>): ReturnType<MessagingApi["getCustomerConversation"]> {
    return this.messaging.getCustomerConversation(...args);
  }

  sendCustomerMessage(...args: Parameters<MessagingApi["sendCustomerMessage"]>): ReturnType<MessagingApi["sendCustomerMessage"]> {
    return this.messaging.sendCustomerMessage(...args);
  }

  markCustomerConversationRead(...args: Parameters<MessagingApi["markCustomerConversationRead"]>): ReturnType<MessagingApi["markCustomerConversationRead"]> {
    return this.messaging.markCustomerConversationRead(...args);
  }

  listTenantConversations(...args: Parameters<MessagingApi["listTenantConversations"]>): ReturnType<MessagingApi["listTenantConversations"]> {
    return this.messaging.listTenantConversations(...args);
  }

  getTenantMessageUnreadCount(...args: Parameters<MessagingApi["getTenantMessageUnreadCount"]>): ReturnType<MessagingApi["getTenantMessageUnreadCount"]> {
    return this.messaging.getTenantMessageUnreadCount(...args);
  }

  getTenantConversation(...args: Parameters<MessagingApi["getTenantConversation"]>): ReturnType<MessagingApi["getTenantConversation"]> {
    return this.messaging.getTenantConversation(...args);
  }

  sendTenantMessage(...args: Parameters<MessagingApi["sendTenantMessage"]>): ReturnType<MessagingApi["sendTenantMessage"]> {
    return this.messaging.sendTenantMessage(...args);
  }

  markTenantConversationRead(...args: Parameters<MessagingApi["markTenantConversationRead"]>): ReturnType<MessagingApi["markTenantConversationRead"]> {
    return this.messaging.markTenantConversationRead(...args);
  }

  getTenantMessageTemplates(...args: Parameters<MessagingApi["getTenantMessageTemplates"]>): ReturnType<MessagingApi["getTenantMessageTemplates"]> {
    return this.messaging.getTenantMessageTemplates(...args);
  }

  getNotificationPreferences(...args: Parameters<MessagingApi["getNotificationPreferences"]>): ReturnType<MessagingApi["getNotificationPreferences"]> {
    return this.messaging.getNotificationPreferences(...args);
  }

  updateNotificationPreferences(...args: Parameters<MessagingApi["updateNotificationPreferences"]>): ReturnType<MessagingApi["updateNotificationPreferences"]> {
    return this.messaging.updateNotificationPreferences(...args);
  }

  getMessagingSettings(...args: Parameters<MessagingApi["getMessagingSettings"]>): ReturnType<MessagingApi["getMessagingSettings"]> {
    return this.messaging.getMessagingSettings(...args);
  }

  updateMessagingSettings(...args: Parameters<MessagingApi["updateMessagingSettings"]>): ReturnType<MessagingApi["updateMessagingSettings"]> {
    return this.messaging.updateMessagingSettings(...args);
  }

  getBusinessWhatsAppLink(...args: Parameters<MessagingApi["getBusinessWhatsAppLink"]>): ReturnType<MessagingApi["getBusinessWhatsAppLink"]> {
    return this.messaging.getBusinessWhatsAppLink(...args);
  }

  getBookingWhatsAppLink(...args: Parameters<MessagingApi["getBookingWhatsAppLink"]>): ReturnType<MessagingApi["getBookingWhatsAppLink"]> {
    return this.messaging.getBookingWhatsAppLink(...args);
  }
}
