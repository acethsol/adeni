# Adeni Subscription & Pricing — Cursor Build Package
Version: 2026-10-19
Status: Build specification for current Adeni Wellness launch

## Purpose
This package defines subscription, pricing, trials, discounts, plan entitlements, billing-country resolution, price versioning, GMV platform-fee readiness, and admin commercial controls.

Where this package conflicts with older Adeni pricing/subscription notes, **this package wins**.

## Current product context
- Product: Adeni Beauty & Wellness
- Launch markets: Ottawa, Canada and Lagos, Nigeria
- Backend: .NET
- Database: SQL Server
- Web: Angular (Discover, Portal, Admin)
- Flutter mobile app later
- Carbon Design System for UI

## Important boundary
Keep these separate:
1. Business → Adeni SaaS subscription billing
2. Customer → Business service payments through Adeni checkout

Adeni should remain **non-custodial** for customer payments.

## Build philosophy
- Build launch rules now.
- Make pricing/configuration data-driven.
- Preserve historical prices.
- Never silently reprice existing customers.
- No permanent free plan.
- Do not over-engineer annual billing, AI add-ons, financing, or insurance yet.
