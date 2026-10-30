import type { LocaleId } from "@adeni/shared";

export type ChromeKey =
  | "search"
  | "appearance"
  | "language"
  | "light"
  | "dark"
  | "system"
  | "signOut"
  | "settings"
  | "jump"
  | "marketplace"
  | "discoverHint"
  | "groupToday"
  | "groupListing"
  | "groupBusiness"
  | "tabPending"
  | "tabUpcoming"
  | "tabPast"
  | "tabDetails"
  | "tabOverview"
  | "tabBusiness"
  | "tabReviews"
  | "tabVerification"
  | "tabInbox"
  | "tabWhatsapp"
  | "tabLinks"
  | "tabLedger"
  | "tabBookings"
  | "tabNotifications"
  | "tabPolicies"
  | "tabMessaging"
  | "tabWalkIns"
  | "tabPackages"
  | "tabMemberships"
  | "tabLoyalty"
  | "businessSettings"
  | "notificationSettings"
  | "chromePreferences"
  | "locationSwitch"
  | "allLocations"
  | "/dashboard"
  | "/bookings"
  | "/customers"
  | "/queue"
  | "/calendar"
  | "/my-calendar"
  | "/quotes"
  | "/messages"
  | "/services"
  | "/staff"
  | "/locations"
  | "/availability"
  | "/profile"
  | "/public-page"
  | "/register"
  | "/finance"
  | "/retention"
  | "/settings"
  | "/plan"
  | "/__discover";

export const CHROME: Record<LocaleId, Record<ChromeKey, string>> = {
  en: {
    search: "Search",
    appearance: "Appearance",
    language: "Language",
    light: "Light",
    dark: "Dark",
    system: "Match system",
    signOut: "Sign out",
    settings: "Settings",
    jump: "Jump to",
    marketplace: "Discover",
    discoverHint: "Browse Adeni as customers see it (opens in a new tab)",
    groupToday: "Operations",
    groupListing: "Setup",
    groupBusiness: "Account",
    tabPending: "Pending",
    tabUpcoming: "Upcoming",
    tabPast: "Past",
    tabDetails: "Details",
    tabOverview: "Overview",
    tabBusiness: "Business details",
    tabReviews: "Reviews",
    tabVerification: "Verification",
    tabInbox: "Inbox",
    tabWhatsapp: "WhatsApp",
    tabLinks: "Links",
    tabLedger: "Ledger",
    tabBookings: "Bookings",
    tabNotifications: "Notifications",
    tabPolicies: "Policies",
    tabMessaging: "Messaging",
    tabWalkIns: "Walk-ins",
    tabPackages: "Packages",
    tabMemberships: "Memberships",
    tabLoyalty: "Loyalty",
    businessSettings: "Business settings",
    notificationSettings: "Notification preferences",
    chromePreferences: "Appearance & language",
    locationSwitch: "Location",
    allLocations: "All locations",
    "/dashboard": "Dashboard",
    "/bookings": "Bookings",
    "/customers": "Customers",
    "/queue": "Queue",
    "/calendar": "Calendar",
    "/my-calendar": "My calendar",
    "/quotes": "Quotes",
    "/messages": "Messages",
    "/services": "Services",
    "/staff": "Staff",
    "/locations": "Locations",
    "/availability": "Hours",
    "/profile": "Profile",
    "/public-page": "Public page",
    "/register": "Register",
    "/finance": "Finance",
    "/retention": "Retention",
    "/settings": "Settings",
    "/plan": "Plan",
    "/__discover": "Discover",
  },
  fr: {
    search: "Rechercher",
    appearance: "Apparence",
    language: "Langue",
    light: "Clair",
    dark: "Sombre",
    system: "Système",
    signOut: "Se déconnecter",
    settings: "Paramètres",
    jump: "Aller à",
    marketplace: "Découvrir",
    discoverHint: "Parcourir Adeni comme un client (nouvel onglet)",
    groupToday: "Opérations",
    groupListing: "Configuration",
    groupBusiness: "Compte",
    tabPending: "En attente",
    tabUpcoming: "À venir",
    tabPast: "Passées",
    tabDetails: "Détails",
    tabOverview: "Aperçu",
    tabBusiness: "Informations",
    tabReviews: "Avis",
    tabVerification: "Vérification",
    tabInbox: "Boîte",
    tabWhatsapp: "WhatsApp",
    tabLinks: "Liens",
    tabLedger: "Journal",
    tabBookings: "Réservations",
    tabNotifications: "Notifications",
    tabPolicies: "Politiques",
    tabMessaging: "Messagerie",
    tabWalkIns: "Sans rendez-vous",
    tabPackages: "Forfaits",
    tabMemberships: "Abonnements",
    tabLoyalty: "Fidélité",
    businessSettings: "Paramètres de l'entreprise",
    notificationSettings: "Préférences de notification",
    chromePreferences: "Apparence et langue",
    locationSwitch: "Emplacement",
    allLocations: "Tous les emplacements",
    "/dashboard": "Tableau de bord",
    "/bookings": "Réservations",
    "/customers": "Clients",
    "/queue": "File",
    "/calendar": "Calendrier",
    "/my-calendar": "Mon calendrier",
    "/quotes": "Devis",
    "/messages": "Messages",
    "/services": "Services",
    "/staff": "Équipe",
    "/locations": "Lieux",
    "/availability": "Horaires",
    "/profile": "Profil",
    "/public-page": "Page publique",
    "/register": "Inscription",
    "/finance": "Finance",
    "/retention": "Fidélisation",
    "/settings": "Paramètres",
    "/plan": "Forfait",
    "/__discover": "Découvrir",
  },
};

